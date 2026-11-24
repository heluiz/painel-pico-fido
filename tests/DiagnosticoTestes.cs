using System;
using System.IO;
using System.Text;

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

        var resultado = new StringBuilder();
        PicoPanel.AppendDiagnostic(resultado, "USB", () => { throw new UnauthorizedAccessException("acesso negado"); });
        PicoPanel.AppendDiagnostic(resultado, "CCID", () => PicoPanel.CcidStatus(2));
        PicoPanel.AppendDiagnostic(resultado, "BOOTSEL", () => { throw new IOException("unidade indisponível"); });

        string relatorio = resultado.ToString();
        Contem(relatorio, "Consulta indisponível: acesso negado");
        Contem(relatorio, "2 leitores candidatos");
        Contem(relatorio, "Consulta indisponível: unidade indisponível");

        Console.WriteLine("Testes do diagnóstico aprovados.");
    }
}
