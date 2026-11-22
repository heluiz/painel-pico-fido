# Painel Pico FIDO

Painel Windows com tema verde de alto contraste para Raspberry Pi Pico 2 com Pico FIDO 8.0.

- **Chave e contas**: diagnóstico USB, PIN, credenciais FIDO, contas OATH e códigos TOTP.
- **LED e USB**: modo do LED, nome USB, VID/PID, interface de teclado e curva secp256k1. Brilho ajustável somente com driver WS2812 ativo.
- **Slots OTP**: quatro slots com texto digitado, HOTP, HMAC-SHA1 ou Yubico OTP. Informe o código de acesso atual se o slot estiver protegido.
- **Avançado**: firmware, memória, consulta de Secure Boot e Secure Lock, e sincronização do relógio.

Em LED e USB, leia os ajustes, edite e aplique. Pressione BOOTSEL quando a placa pedir confirmação e reconecte depois de gravar. Use somente VID/PID atribuídos a você. O gerenciamento requer uma única Pico com interface CCID ativa.

O painel verifica o firmware UF2 oficial. Não grava firmware nem programa fusíveis.

## Compilar

No PowerShell, na pasta do projeto:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /out:PainelPicoFido.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Management.dll PainelPicoFido.cs PicoCard.cs PicoOath.cs PicoSettings.cs HardwareViews.cs
```

Abra PainelPicoFido.exe.

Firmware oficial: https://github.com/polhenarejos/pico-fido/releases/tag/v8.0
