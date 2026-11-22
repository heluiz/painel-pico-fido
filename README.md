# Painel Pico FIDO

Painel Windows para Raspberry Pi Pico 2 com Pico FIDO 8.0.

- Diagnóstico USB e detecção do modo BOOTSEL.
- Verificação SHA-256 do arquivo UF2 oficial.
- Atalhos para PIN no Windows e credenciais da chave no Chrome.

O painel não grava firmware. A disponibilidade das funções do Chrome varia com a versão do navegador e do Windows.

Para compilar, use o compilador C# do .NET Framework 4, com as referências System.Windows.Forms, System.Drawing e System.Management. Compile PainelPicoFido.cs como aplicativo Windows.

Firmware oficial: https://github.com/polhenarejos/pico-fido/releases/tag/v8.0
