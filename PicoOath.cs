using System;
using System.Collections.Generic;
using System.Linq;

using System.Text;

static class PicoOath
{
    static T Access<T>(Func<IntPtr, uint, byte[], T> action)
    {
        return PicoCard.Open((card, protocol) => {
            var selected = PicoCard.Send(card, protocol, 0, 0xA4, 4, 0, 7, 0xA0, 0, 0, 5, 0x27, 0x21, 1);
            if (selected[selected.Length - 2] != 0x90 || selected[selected.Length - 1] != 0)
                throw new InvalidOperationException("A Pico recusou a seleção do aplicativo OATH.");
            var data = new List<byte>();
            var reply = PicoCard.Send(card, protocol, 0, 0xA1, 0, 0);
            for (int page = 0; page < 64; page++)
            {
                int end = reply.Length - 2;
                data.AddRange(reply.Take(end));
                if (reply[end] == 0x90 && reply[end + 1] == 0)
                    return action(card, protocol, data.ToArray());
                if (reply[end] != 0x61)
                    throw new InvalidOperationException(string.Format("A Pico recusou a listagem OATH ({0:X2}{1:X2}).", reply[end], reply[end+1]));
                reply = PicoCard.Send(card, protocol, 0, 0xA5, 0, 0);
            }
            throw new InvalidOperationException("A listagem OATH excedeu o limite de páginas.");
        });
    }

    internal static KeyValuePair<string, string>[] Accounts()
    {
        return Access((card, protocol, data) => ReadAccounts(data).ToArray());
    }

    public static string ListAccounts()
    {
        var entries = Accounts();
        return entries.Length == 0 ? "Nenhuma conta OATH cadastrada." :
            string.Join(Environment.NewLine, entries.Select(a => a.Key + ": " + a.Value));
    }

    public static string AddTotp(string label, string base32)
    {
        byte[] name = Encoding.UTF8.GetBytes(label.Trim());
        if (name.Length < 1 || name.Length > 64 || label.Any(char.IsControl))
            throw new InvalidOperationException("Informe um nome válido de até 64 bytes UTF-8.");
        byte[] key = DecodeBase32(base32);
        try
        {
            return Access((card, protocol, data) => {
                if (ReadAccounts(data).Any(a => a.Value == label.Trim()))
                    throw new InvalidOperationException("Já existe uma conta OATH com esse nome.");
                byte[] command = new byte[11 + name.Length + key.Length];
                command[1] = 1;
                command[4] = (byte)(command.Length - 5);
                command[5] = 0x71;
                command[6] = (byte)name.Length;
                Buffer.BlockCopy(name, 0, command, 7, name.Length);
                int i = 7 + name.Length;
                Array.Resize(ref command, i + 4 + key.Length);
                command[4] = (byte)(command.Length - 5);
                command[i] = 0x73;
                command[i + 1] = (byte)(key.Length + 2);
                command[i + 2] = 0x21;
                command[i + 3] = 6;
                Buffer.BlockCopy(key, 0, command, i + 4, key.Length);
                try
                {
                    var reply = PicoCard.Send(card, protocol, command);
                    int end = reply.Length - 2;
                    if (reply[end] != 0x90 || reply[end + 1] != 0)
                        throw new InvalidOperationException(string.Format("A Pico recusou o cadastro ({0:X2}{1:X2}).", reply[end], reply[end + 1]));
                    return "Conta TOTP adicionada. Verifique o código no serviço antes de concluir o cadastro.";
                }
                finally { Array.Clear(command, 0, command.Length); }
            });
        }
        finally { Array.Clear(key, 0, key.Length); }
    }

    public static string[] TotpNames()
    {
        return Accounts().Where(a => a.Key == "TOTP").Select(a => a.Value).ToArray();
    }

    public static string CurrentCode(string label)
    {
        return Access((card, protocol, data) => {
            if (!ReadAccounts(data).Any(a => a.Key == "TOTP" && a.Value == label))
                throw new InvalidOperationException("Conta TOTP não encontrada.");
            byte[] name = Encoding.UTF8.GetBytes(label);
            if (name.Length > 120) throw new InvalidOperationException("Nome OATH longo demais.");
            long seconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            byte[] counter = BitConverter.GetBytes(seconds / 30);
            if (BitConverter.IsLittleEndian) Array.Reverse(counter);
            var command = new List<byte> { 0, 0xA2, 0, 1, (byte)(12 + name.Length), 0x71, (byte)name.Length };
            command.AddRange(name);
            command.AddRange(new byte[] { 0x74, 8 });
            command.AddRange(counter);
            byte[] reply = PicoCard.Send(card, protocol, command.ToArray());
            if (reply.Length != 9 || reply[0] != 0x76 || reply[1] != 5 ||
                reply[2] < 6 || reply[2] > 8 || reply[7] != 0x90 || reply[8] != 0)
                throw new InvalidOperationException("A Pico não retornou um código TOTP. A conta pode exigir confirmação física ou autenticação.");
            int value = ((reply[3] & 127) << 24) | (reply[4] << 16) | (reply[5] << 8) | reply[6];
            int modulus = (int)Math.Pow(10, reply[2]);
            int remaining = Math.Max(0, 30 - (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - seconds / 30 * 30));
            return (value % modulus).ToString("D" + reply[2]) + "  |  Válido por cerca de " + remaining + " s ao abrir";
        });
    }

    static byte[] DecodeBase32(string input)
    {
        string value = input.Trim().Replace(" ", "").TrimEnd('=').ToUpperInvariant();
        if (value.Length < 16 || value.Length > 104)
            throw new InvalidOperationException("Informe uma chave Base32 de 16 a 104 caracteres.");
        var bytes = new byte[value.Length * 5 / 8];
        int bits = 0, buffer = 0, index = 0;
        foreach (char c in value)
        {
            int digit = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".IndexOf(c);
            if (digit < 0) throw new InvalidOperationException("Chave Base32 inválida.");
            buffer = (buffer << 5) | digit;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes[index++] = (byte)(buffer >> bits);
                buffer &= (1 << bits) - 1;
            }
        }
        if (bits >= 5 || buffer != 0) throw new InvalidOperationException("Comprimento ou final Base32 inválido.");
        return bytes;
    }

    static List<KeyValuePair<string,string>> ReadAccounts(byte[] data)
    {
        var accounts = new List<KeyValuePair<string,string>>();
        for (int i = 0; i < data.Length;)
        {
            if (data.Length - i < 3 || data[i++] != 0x72)
                throw new InvalidOperationException("Formato OATH inesperado.");
            int size = data[i++];
            if (size < 1 || size > data.Length - i)
                throw new InvalidOperationException("Registro OATH incompleto.");
            int type = data[i] & 0xF0;
            string prefix = type == 0x20 ? "TOTP" : type == 0x10 ? "HOTP" : "OATH";
            accounts.Add(new KeyValuePair<string,string>(prefix, Encoding.UTF8.GetString(data, i + 1, size - 1)));
            i += size;
        }
        return accounts;
    }
}
