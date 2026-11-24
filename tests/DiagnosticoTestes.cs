using System;
using System.IO;
using System.Collections.Generic;

static class DiagnosticoTestes
{
    static void Contem(string resultado, string trecho)
    {
        if (!resultado.Contains(trecho))
            throw new Exception("Trecho esperado: " + trecho + Environment.NewLine + resultado);
    }

    static void Main()
    {
        Contem(PicoPanel.CcidStatus(0), "Nenhum leitor");
        Contem(PicoPanel.CcidStatus(1), "identidade da chave não foi verificada");
        Contem(PicoPanel.CcidStatus(2), "2 leitores candidatos");

        Contem(PicoPanel.ReadDiagnostic(() => { throw new UnauthorizedAccessException("acesso negado"); }),
            "Consulta indisponível: acesso negado");
        Contem(PicoPanel.ReadDiagnostic(() => PicoPanel.CcidStatus(2)), "2 leitores candidatos");
        Contem(PicoPanel.ReadDiagnostic(() => { throw new IOException("unidade indisponível"); }),
            "Consulta indisponível: unidade indisponível");

        var original = new SortedDictionary<byte, byte[]> { { 0, new byte[] { 1 } }, { 9, new byte[] { 65 } } };
        var iguais = new SortedDictionary<byte, byte[]> { { 0, new byte[] { 1 } }, { 9, new byte[] { 65 } } };
        if (HardwareViews.ChangedSettings(original, iguais).Length != 0)
            throw new Exception("Valores iguais foram identificados como alterações.");
        iguais.Remove(0);
        iguais[9] = new byte[] { 66 };
        iguais[11] = new byte[] { 8 };
        string mudancas = string.Join(", ", HardwareViews.ChangedSettings(original, iguais));
        if (mudancas != "VID/PID, Nome USB, Interface de teclado OTP")
            throw new Exception("Resumo inesperado: " + mudancas);

        string uf2 = Path.GetTempFileName();
        try
        {
            Contem(PicoPanel.CheckFirmware(uf2), "O tamanho difere");
            File.WriteAllBytes(uf2, new byte[893440]);
            Contem(PicoPanel.CheckFirmware(uf2), "O SHA-256 difere");
        }
        finally { File.Delete(uf2); }

        Console.WriteLine("Testes do diagnóstico aprovados.");
    }
}
