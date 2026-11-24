using System;
using System.Linq;
using System.Runtime.InteropServices;

static class PicoCard
{
    [DllImport("winscard.dll")] static extern int SCardEstablishContext(uint scope, IntPtr a, IntPtr b, out IntPtr context);
    [DllImport("winscard.dll")] static extern int SCardReleaseContext(IntPtr context);
    [DllImport("winscard.dll", CharSet=CharSet.Unicode)] static extern int SCardListReaders(IntPtr context, string groups, char[] readers, ref uint count);
    [DllImport("winscard.dll", CharSet=CharSet.Unicode)] static extern int SCardConnect(IntPtr context, string reader, uint share, uint protocols, out IntPtr card, out uint protocol);
    [DllImport("winscard.dll")] static extern int SCardDisconnect(IntPtr card, uint disposition);
    [StructLayout(LayoutKind.Sequential)] struct IoRequest { public uint Protocol; public uint Length; }
    [DllImport("winscard.dll")] static extern int SCardTransmit(IntPtr card, ref IoRequest pci, byte[] message, uint length, IntPtr receivePci, byte[] response, ref uint responseLength);

    static void Check(int result)
    {
        if (result != 0) throw new InvalidOperationException("Erro do leitor de cartão: " + result.ToString("X8"));
    }

    internal static byte[] Send(IntPtr card, uint protocol, params byte[] command)
    {
        var pci = new IoRequest { Protocol = protocol, Length = 8 };
        var response = new byte[4096];
        uint size = (uint)response.Length;
        Check(SCardTransmit(card, ref pci, command, (uint)command.Length, IntPtr.Zero, response, ref size));
        if (size < 2) throw new InvalidOperationException("Resposta do cartão incompleta.");
        return response.Take((int)size).ToArray();
    }

    static string[] ListCandidates(IntPtr context)
    {
        const int noReaders = unchecked((int)0x8010002E);
        uint count = 0;
        int result = SCardListReaders(context, null, null, ref count);
        if (result == noReaders) return new string[0];
        Check(result);
        var names = new char[count];
        result = SCardListReaders(context, null, names, ref count);
        if (result == noReaders) return new string[0];
        Check(result);
        return new string(names).Split('\0').Where(name =>
            name.StartsWith("Pol Henarejos ", StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    internal static int CountCandidates()
    {
        IntPtr context;
        Check(SCardEstablishContext(0, IntPtr.Zero, IntPtr.Zero, out context));
        try { return ListCandidates(context).Length; }
        finally { SCardReleaseContext(context); }
    }

    internal static T Open<T>(Func<IntPtr, uint, T> action)
    {
        IntPtr context;
        Check(SCardEstablishContext(0, IntPtr.Zero, IntPtr.Zero, out context));
        try
        {
            var readers = ListCandidates(context);
            if (readers.Length != 1)
                throw new InvalidOperationException("Conecte exatamente uma PicoKey com interface CCID.");
            IntPtr card;
            uint protocol;
            Check(SCardConnect(context, readers[0], 1, 3, out card, out protocol));
            try
            {
                return action(card, protocol);
            }
            finally { SCardDisconnect(card, 0); }
        }
        finally { SCardReleaseContext(context); }
    }

}
