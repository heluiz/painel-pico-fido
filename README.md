# Painel Pico FIDO

Painel Windows com tema verde de alto contraste para Raspberry Pi Pico 2 com Pico FIDO 8.0.

## Diagnóstico somente leitura

Abra `PainelPicoFido.exe` e clique em **Atualizar diagnóstico**. O relatório mostra separadamente as interfaces com VID/PID padrão, os leitores CCID candidatos pelo nome e os volumes identificados como RP2350 em modo BOOTSEL. Cada consulta pode retornar seu próprio erro. O painel enumera informações do Windows sem abrir sessão com a chave, enviar comandos à Pico ou pedir PIN. Um leitor candidato não comprova a identidade da chave e um VID/PID personalizado pode não aparecer na seção USB.

A opção `--diagnostics caminho.txt` gera o mesmo relatório em arquivo.

## Gerenciamento

Para abrir as ferramentas de gerenciamento existentes, execute `.\PainelPicoFido.exe --gerenciar` no PowerShell.

- **Chave e contas**: diagnóstico USB, PIN, credenciais FIDO, contas OATH e códigos TOTP.
- **LED e USB**: modo do LED, nome USB, VID/PID, interface de teclado e curva secp256k1. Brilho ajustável somente com driver WS2812 ativo.
- **Slots OTP**: quatro slots com texto digitado, HOTP, HMAC-SHA1 ou Yubico OTP. Informe o código de acesso atual se o slot estiver protegido.
- **Avançado**: firmware, memória, consulta de Secure Boot e Secure Lock, e sincronização do relógio.

Em LED e USB, leia os ajustes, edite e aplique. Pressione BOOTSEL quando a placa pedir confirmação e reconecte depois de gravar. Use somente VID/PID atribuídos a você. O gerenciamento requer uma única Pico com interface CCID ativa.

O painel verifica o firmware UF2 oficial. Não grava firmware nem programa fusíveis.

## Compilar

No PowerShell, na pasta do projeto:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /codepage:65001 /target:winexe /out:PainelPicoFido.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Management.dll PainelPicoFido.cs PicoCard.cs PicoOath.cs PicoSettings.cs HardwareViews.cs
```

## Testar sem hardware

Execute `.\tests\Testar.ps1` para validar os estados de leitores e o relatório parcial com falhas simuladas. Depois de compilar o painel, os argumentos `--smoke-diagnostico` e `--smoke-ui` abrem e fecham as telas sem disparar consultas ao Windows ou à chave.

## Referências

- [PicoForge](https://github.com/librekeys/picoforge/tree/d085608), AGPL-3.0-only. Seu código separa os transportes FIDO e PC/SC e confirma a identidade do firmware antes de correlacionar interfaces. O Painel adotou a separação das observações, com enumeração local sem confirmação de identidade. O PicoForge recomenda RS-Key no RP2350 e a troca de firmware está fora desta versão do Painel.
- [PicoKey App](https://github.com/polhenarejos/picokeyapp/tree/0f12a0f), distribuição binária sob EULA. A documentação pública serviu como referência para distinguir inspeção, configuração e atualização de firmware.

O diagnóstico usa a implementação existente do Painel e código original. Nenhum código, recurso visual ou binário dos projetos de referência foi incorporado.

Firmware oficial: https://github.com/polhenarejos/pico-fido/releases/tag/v8.0
