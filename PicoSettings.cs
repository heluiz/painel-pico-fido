using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

static class PicoSettings
{
    static readonly byte[] Rescue = { 0xA0, 0x58, 0x3F, 0xC1, 0x9B, 0x7E, 0x4F, 0x21 };
    static readonly byte[] Otp = { 0xA0, 0, 0, 5, 0x27, 0x20, 1 };

    static byte[] Exchange(IntPtr card, uint protocol, byte[] command)
    {
        byte[] answer = PicoCard.Send(card, protocol, command);
        int end = answer.Length - 2;
        if (answer[end] != 0x90 || answer[end + 1] != 0)
            throw new InvalidOperationException(string.Format(
                "A Pico recusou o comando ({0:X2}{1:X2}).", answer[end], answer[end + 1]));
        return answer.Take(end).ToArray();
    }

    static byte[] Command(IntPtr card, uint protocol, byte cla, byte ins, byte p1, byte p2, byte[] data = null)
    {
        var command = new List<byte> { cla, ins, p1, p2 };
        if (data != null)
        {
            if (data.Length > 255) throw new InvalidOperationException("Comando longo demais.");
            command.Add((byte)data.Length);
            command.AddRange(data);
        }
        byte[] packet = command.ToArray();
        command.Clear();
        try { return Exchange(card, protocol, packet); }
        finally { Array.Clear(packet, 0, packet.Length); }
    }

    static byte[] SelectRescue(IntPtr card, uint protocol)
    {
        byte[] version = Command(card, protocol, 0, 0xA4, 4, 0, Rescue);
        if (version.Length < 4 || version[0] != 1 || version[1] != 2 || version[2] != 8 || version[3] != 0)
            throw new InvalidOperationException("Estas configurações exigem Pico FIDO 8.0 em uma placa RP2350.");
        return version;
    }

    static SortedDictionary<byte, byte[]> ReadPhy(IntPtr card, uint protocol)
    {
        var values = new SortedDictionary<byte, byte[]>();
        byte[] raw = Command(card, protocol, 0x80, 0x1E, 1, 0);
        for (int i = 0; i < raw.Length;)
        {
            if (raw.Length - i < 2) throw new InvalidOperationException("Configuração USB incompleta.");
            byte tag = raw[i++];
            int length = raw[i++];
            if (length > raw.Length - i || values.ContainsKey(tag))
                throw new InvalidOperationException("Configuração USB inválida.");
            values.Add(tag, raw.Skip(i).Take(length).ToArray());
            i += length;
        }
        return values;
    }

    internal static SortedDictionary<byte, byte[]> Configuration()
    {
        return PicoCard.Open((card, protocol) => {
            SelectRescue(card, protocol);
            return ReadPhy(card, protocol);
        });
    }

    internal static void Apply(SortedDictionary<byte, byte[]> original, SortedDictionary<byte, byte[]> updated)
    {
        PicoCard.Open((card, protocol) => {
            SelectRescue(card, protocol);
            if (!Encode(ReadPhy(card, protocol)).SequenceEqual(Encode(original)))
                throw new InvalidOperationException("A configuração da placa mudou. Reabra o painel e tente novamente.");
            Command(card, protocol, 0x80, 0x1C, 1, 0, Encode(updated));
            var saved = ReadPhy(card, protocol);
            foreach (var item in updated)
                if (!saved.ContainsKey(item.Key) || !saved[item.Key].SequenceEqual(item.Value))
                    throw new InvalidOperationException("A leitura após gravar não confirmou todas as opções.");
            if (original.Keys.Except(updated.Keys).Any(k => saved.ContainsKey(k)))
                throw new InvalidOperationException("A Pico não confirmou a restauração dos campos removidos.");
            return true;
        });
    }

    static byte[] Encode(SortedDictionary<byte, byte[]> values)
    {
        return values.SelectMany(item => new byte[] { item.Key, (byte)item.Value.Length }.Concat(item.Value)).ToArray();
    }

    static uint U32(byte[] b, int i)
    {
        return ((uint)b[i] << 24) | ((uint)b[i + 1] << 16) | ((uint)b[i + 2] << 8) | b[i + 3];
    }

    internal static string SecurityStatus()
    {
        return PicoCard.Open((card, protocol) => {
            byte[] version = SelectRescue(card, protocol);
            byte[] flash = Command(card, protocol, 0x80, 0x1E, 2, 0);
            byte[] boot = Command(card, protocol, 0x80, 0x1E, 3, 0);
            if (flash.Length < 20 || boot.Length != 3)
                throw new InvalidOperationException("Resposta de diagnóstico incompleta.");
            return string.Format("Firmware: {0}.{1} • RP2350\r\nFlash livre: {2:N0} bytes de {3:N0}\r\nSecure Boot: {4}\r\nSecure Lock: {5}",
                version[2], version[3], U32(flash, 0), U32(flash, 8), boot[0] == 0 ? "desativado" : "ativado",
                boot[1] == 0 ? "desativado" : "ativado");
        });
    }

    internal static void SyncClock()
    {
        PicoCard.Open((card, protocol) => {
            SelectRescue(card, protocol);
            uint seconds = checked((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            byte[] stamp = { (byte)(seconds >> 24), (byte)(seconds >> 16), (byte)(seconds >> 8), (byte)seconds };
            Command(card, protocol, 0x80, 0x1C, 2, 2, stamp);
            byte[] result = Command(card, protocol, 0x80, 0x1E, 4, 2);
            if (result.Length != 4 || U32(result, 0) < seconds || U32(result, 0) - seconds > 5)
                throw new InvalidOperationException("O relógio retornou um valor inesperado.");
            return true;
        });
    }

    static bool[] ReadSlots(IntPtr card, uint protocol)
    {
        SelectRescue(card, protocol);
        Command(card, protocol, 0, 0xA4, 4, 0, Otp);
        byte[] data = Command(card, protocol, 0, 1, 0x14, 0);
        var used = new bool[4];
        for (int i = 0; i < data.Length;)
        {
            if (data.Length - i < 2) throw new InvalidOperationException("Resposta dos slots incompleta.");
            int slot = data[i++] - 0xB0;
            int len = data[i++];
            if (slot < 0 || slot >= used.Length || used[slot] || len > data.Length - i)
                throw new InvalidOperationException("Resposta dos slots inválida.");
            used[slot] = true;
            i += len;
        }
        return used;
    }

    internal static bool[] Slots() { return PicoCard.Open(ReadSlots); }

    internal static void SetSlot(int slot, int mode, string secret, string code, bool replace, bool touch)
    {
        if (slot < 0 || slot > 3 || mode < 0 || mode > 4) throw new ArgumentOutOfRangeException();
        var data = new byte[58];
        byte[] key = null;
        byte[] access = null;
        try
        {
            access = code.Length == 0 ? new byte[6] : Hex(code, 6);
            Buffer.BlockCopy(access, 0, data, 52, 6);
            if (mode == 1)
            {
                if (secret.Length < 1 || secret.Length > 38 || !secret.All(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9'))
                    throw new InvalidOperationException("Use de 1 a 38 letras minúsculas ou números.");
                for (int i = 0; i < secret.Length; i++)
                    data[i] = (byte)(secret[i] >= 'a' ? secret[i] - 'a' + 4 : secret[i] == '0' ? 39 : secret[i] - '1' + 30);
                data[47] = 0x20;
            }
            else if (mode == 2)
            {
                key = Hex(secret, 16);
                Buffer.BlockCopy(key, 0, data, 22, 16);
                data[46] = 0x40;
            }
            else if (mode == 3)
            {
                key = Hex(secret, 20);
                Buffer.BlockCopy(key, 0, data, 22, 16);
                Buffer.BlockCopy(key, 16, data, 16, 4);
                data[46] = 0x40;
                data[47] = (byte)(0x26 | (touch ? 0x08 : 0));
            }
            else if (mode == 4)
            {
                key = Hex(secret, 28);
                Buffer.BlockCopy(key, 0, data, 0, 6);
                Buffer.BlockCopy(key, 6, data, 16, 22);
                data[44] = 6;
            }
            if (mode != 0)
            {
                Buffer.BlockCopy(access, 0, data, 38, 6);
                ushort crc = 0xFFFF;
                for (int i = 0; i < 50; i++)
                {
                    crc ^= data[i];
                    for (int bit = 0; bit < 8; bit++)
                        crc = (ushort)((crc >> 1) ^ ((crc & 1) != 0 ? 0x8408 : 0));
                }
                crc = (ushort)~crc;
                data[50] = (byte)crc;
                data[51] = (byte)(crc >> 8);
            }
            PicoCard.Open((card, protocol) => {
                bool occupied = ReadSlots(card, protocol)[slot];
                if (occupied != replace) throw new InvalidOperationException("O estado do slot mudou. Atualize a lista.");
                if (mode == 0 && !occupied) return true;
                Command(card, protocol, 0, 1, 1, (byte)slot, data);
                if (ReadSlots(card, protocol)[slot] != (mode != 0))
                    throw new InvalidOperationException("A leitura após gravar não confirmou o estado esperado do slot.");
                return true;
            });
        }
        finally
        {
            Array.Clear(data, 0, data.Length);
            if (key != null) Array.Clear(key, 0, key.Length);
            if (access != null) Array.Clear(access, 0, access.Length);
        }
    }

    static byte[] Hex(string text, int length)
    {
        if (text.Length != length * 2 || !text.All(Uri.IsHexDigit))
            throw new InvalidOperationException("Informe exatamente " + (length * 2) + " dígitos hexadecimais.");
        var bytes = new byte[length];
        for (int i = 0; i < length; i++) bytes[i] = Convert.ToByte(text.Substring(i * 2, 2), 16);
        return bytes;
    }
}
