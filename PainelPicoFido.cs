using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

static class PicoPanel
{
    static string Detect()
    {
        int hid;
        int ready;
        ReadDefaultUsb(new StringBuilder(), out hid, out ready);
        if (hid == 0)
            return "Nenhuma interface HID com VID/PID padrão encontrada. Consulte o diagnóstico para verificar leitores CCID.";
        if (hid > 1)
            return "Várias interfaces HID com VID/PID padrão encontradas. Confira os códigos no diagnóstico.";
        return ready == 1 ? "Interface HID com VID/PID padrão reconhecida pelo Windows." :
            "Interface HID encontrada, mas sem confirmação de funcionamento pelo Windows.";
    }

    static void ReadDefaultUsb(StringBuilder output, out int hid, out int ready)
    {
        hid = ready = 0;
        using (var query = new ManagementObjectSearcher(
            "SELECT Name, PNPDeviceID, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE Present = TRUE AND PNPDeviceID LIKE '%VID_2E8A&PID_10FE%'"))
        using (var results = query.Get())
        foreach (ManagementObject device in results)
        using (device)
        {
            object code = device["ConfigManagerErrorCode"];
            output.AppendLine(string.Format("• {0} (código Windows: {1})",
                device["Name"] ?? "Interface sem nome", code ?? "indisponível"));
            string id = device["PNPDeviceID"] as string;
            if (id != null && id.StartsWith("HID", StringComparison.OrdinalIgnoreCase) &&
                id.IndexOf("VID_2E8A&PID_10FE&MI_00", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                hid++;
                if (code is uint && (uint)code == 0) ready++;
            }
        }
    }

    internal static string CcidStatus(int count)
    {
        if (count == 0) return "Nenhum leitor compatível pelo nome. A interface CCID pode estar desativada ou com nome personalizado.";
        if (count == 1) return "1 leitor candidato encontrado pelo nome. A identidade da chave não foi verificada.";
        return count + " leitores candidatos encontrados pelo nome. A identidade das chaves não foi verificada.";
    }

    static string Diagnostics()
    {
        var output = new StringBuilder();
        AppendDiagnostic(output, "Interfaces USB com VID/PID padrão (2E8A:10FE)", () => {
            var usb = new StringBuilder();
            int hid, ready;
            ReadDefaultUsb(usb, out hid, out ready);
            return usb.Length == 0 ? "Nenhuma interface com o identificador padrão encontrada." : usb.ToString().TrimEnd();
        });
        AppendDiagnostic(output, "Leitores CCID", () => CcidStatus(PicoCard.CountCandidates()));
        AppendDiagnostic(output, "Volumes BOOTSEL", () => {
            int candidates = 0;
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.DriveType == DriveType.Removable && drive.IsReady &&
                        drive.VolumeLabel == "RP2350" &&
                        File.Exists(Path.Combine(drive.Name, "INFO_UF2.TXT")))
                        candidates++;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return candidates == 0 ? "Nenhum volume RP2350 identificado." :
                candidates + " volume(s) com rótulo RP2350 e arquivo INFO_UF2.TXT.";
        });
        return output.ToString();
    }

    internal static void AppendDiagnostic(StringBuilder output, string title, Func<string> read)
    {
        output.AppendLine(title);
        try { output.AppendLine(read()); }
        catch (Exception error)
        {
            if (!(error is ManagementException || error is UnauthorizedAccessException ||
                error is InvalidOperationException || error is IOException ||
                error is System.Runtime.InteropServices.COMException)) throw;
            output.AppendLine("Consulta indisponível: " + error.Message);
        }
        output.AppendLine();
    }

    static void RunDiagnostics(bool smoke)
    {
        using (var form = new Form())
        {
            form.Text = "Painel Pico FIDO — Diagnóstico";
            form.ClientSize = new Size(700, 510);
            form.MinimumSize = new Size(400, 300);
            form.StartPosition = FormStartPosition.CenterScreen;
            form.Padding = new Padding(16);
            form.Font = new Font("Segoe UI", 11);
            var title = new Label {
                Dock = DockStyle.Top, Height = 50,
                Text = "Diagnóstico somente leitura\r\nConsulte as interfaces reconhecidas pelo Windows."
            };
            var report = new TextBox {
                Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                AccessibleName = "Resultado do diagnóstico", Text = "Clique em Atualizar diagnóstico para consultar."
            };
            var refresh = new NeonButton { Dock = DockStyle.Bottom, Height = 42, Text = "Atualizar diagnóstico" };
            refresh.Click += async (sender, e) => {
                refresh.Enabled = false;
                report.Text = "Consultando o Windows…";
                string result = await Task.Run((Func<string>)Diagnostics);
                if (form.IsDisposed) return;
                report.Text = result;
                refresh.Enabled = true;
            };
            form.Controls.AddRange(new Control[] { report, refresh, title });
            Neon.Style(form);
            if (smoke) form.Shown += (sender, e) => form.BeginInvoke((Action)form.Close);
            Application.Run(form);
        }
    }

    static string CheckFirmware(string path)
    {
        const string expected = "97116184551AD3C7510225E3E114F4E36FF09F50BCE1F4E79A72A5C7F778C168";
        using (var input = File.OpenRead(path))
        using (var sha = System.Security.Cryptography.SHA256.Create())
        {
            if (input.Length != 893440)
                return "O tamanho difere do UF2 oficial para Pico 2.";
            string hash = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
            return hash == expected ? "Confirmado: Pico FIDO 8.0 oficial para Raspberry Pi Pico 2." :
                "O SHA-256 difere do UF2 oficial: " + hash;
        }
    }

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--status")
        {
            File.WriteAllText(args[1], Detect());
            return;
        }

        if (args.Length == 2 && args[0] == "--diagnostics")
        {
            File.WriteAllText(args[1], Diagnostics());
            return;
        }
        if (args.Length == 3 && args[0] == "--verify-uf2")
        {
            File.WriteAllText(args[2], CheckFirmware(args[1]));
            return;
        }

        if (args.Length == 2 && args[0] == "--oath")
        {
            try { File.WriteAllText(args[1], PicoOath.ListAccounts()); }
            catch (InvalidOperationException error) { File.WriteAllText(args[1], error.Message); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 2 && args[0] == "--hardware")
        {
            try
            {
                var options = PicoSettings.Configuration();
                var slots = PicoSettings.Slots();
                File.WriteAllText(args[1], PicoSettings.SecurityStatus() + Environment.NewLine +
                    "Campos da placa: " + options.Count + Environment.NewLine +
                    "Slots: " + string.Join(", ", Array.ConvertAll(slots, v => v ? "ocupado" : "livre")));
            }
            catch (InvalidOperationException error) { File.WriteAllText(args[1], error.Message); Environment.ExitCode = 1; }
            return;
        }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        bool managementSmoke = args.Length == 1 && args[0] == "--smoke-ui";
        if (!managementSmoke && (args.Length != 1 || args[0] != "--gerenciar"))
        {
            RunDiagnostics(args.Length == 1 && args[0] == "--smoke-diagnostico");
            return;
        }

        using (var form = new Form())
        {
            form.Text = "Painel Pico FIDO";
            form.ClientSize = new Size(760, 680);
            form.MinimumSize = new Size(560, 320);
            form.StartPosition = FormStartPosition.CenterScreen;
            form.Font = new Font("Segoe UI", 11);
            var panel = new FlowLayoutPanel {
                Dock = DockStyle.Fill, Padding = new Padding(20),
                FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true
            };
            var status = new Label { Width = 620, Height = 52, AccessibleName = "Estado do dispositivo" };
            var refresh = new NeonButton { Text = "Verificar conexão USB", AutoSize = true };
            var manage = new NeonButton { Text = "Abrir configurações do PIN", AutoSize = true };
            var help = new Label {
                Width = 620, Height = 90, Padding = new Padding(0, 10, 0, 0),
                Text = "No Windows, abra Chave de segurança → Gerenciar e siga as instruções na tela. " +
                    "Use a opção de adicionar ou alterar o PIN. Redefinir a chave apaga as credenciais armazenadas."
            };
            Action update = () => {
                try { status.Text = Detect(); }
                catch (ManagementException error) { status.Text = "Falha na detecção USB: " + error.Message; }
                catch (UnauthorizedAccessException) { status.Text = "O Windows negou acesso à consulta de dispositivos."; }
            };
            refresh.Click += (sender, e) => update();
            manage.Click += (sender, e) => {
                try { Process.Start(new ProcessStartInfo("ms-settings:signinoptions") { UseShellExecute = true }); }
                catch (System.ComponentModel.Win32Exception error) {
                    MessageBox.Show(form, error.Message, "Falha ao abrir Configurações");
                }
            };
            var chrome = new NeonButton { Text = "Gerenciar credenciais da chave no Chrome", AutoSize = true };
            var verify = new NeonButton { Text = "Verificar UF2 do Pico FIDO 8.0", AutoSize = true };
            var usb = new NeonButton { Text = "Ver interfaces USB e modo BOOTSEL", AutoSize = true };
            chrome.Click += (sender, e) => {
                try {
                    Process.Start(new ProcessStartInfo("chrome.exe", "chrome://settings/securityKeys") { UseShellExecute = true });
                } catch (System.ComponentModel.Win32Exception error) {
                    MessageBox.Show(form, error.Message, "Não foi possível abrir o Chrome");
                }
            };
            usb.Click += (sender, e) => {
                try { MessageBox.Show(form, Diagnostics(), "Diagnóstico USB"); }
                catch (ManagementException error) { MessageBox.Show(form, error.Message, "Falha no diagnóstico"); }
            };
            verify.Click += (sender, e) => {
                using (var picker = new OpenFileDialog { Filter = "Firmware UF2 (*.uf2)|*.uf2" })
                {
                    if (picker.ShowDialog(form) != DialogResult.OK) return;
                    try { MessageBox.Show(form, CheckFirmware(picker.FileName), "Verificação do firmware"); }
                    catch (IOException error) { MessageBox.Show(form, error.Message, "Falha ao ler arquivo"); }
                    catch (UnauthorizedAccessException error) { MessageBox.Show(form, error.Message, "Falha ao ler arquivo"); }
                }
            };
            var oath = new NeonButton { Text = "Listar contas TOTP e HOTP da Pico", AutoSize = true };
            oath.Click += (sender, e) => {
                try { MessageBox.Show(form, PicoOath.ListAccounts(), "Contas OATH"); }
                catch (InvalidOperationException error) { MessageBox.Show(form, error.Message, "Contas OATH"); }
            };
            var addTotp = new NeonButton { Text = "Adicionar conta TOTP", AutoSize = true };
            addTotp.Click += (sender, e) => {
                using (var dialog = new Form { Text = "Adicionar TOTP (SHA-1, 6 dígitos, 30 s)", ClientSize = new Size(440, 180), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false })
                using (var label = new TextBox { Width = 395, AccessibleName = "Nome da conta" })
                using (var secret = new TextBox { Width = 395, UseSystemPasswordChar = true, AccessibleName = "Chave Base32" })
                using (var save = new NeonButton { Text = "Salvar na Pico", AutoSize = true, DialogResult = DialogResult.OK })
                {
                    var fields = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), FlowDirection = FlowDirection.TopDown, WrapContents = false };
                    fields.Controls.AddRange(new Control[] { new Label { Text = "Nome da conta", AutoSize = true }, label, new Label { Text = "Chave Base32 fornecida pelo serviço", AutoSize = true }, secret, save });
                    dialog.Controls.Add(fields);
                    dialog.AcceptButton = save; Neon.Style(dialog);
                    if (dialog.ShowDialog(form) != DialogResult.OK) return;
                    try { MessageBox.Show(form, PicoOath.AddTotp(label.Text, secret.Text), "Cadastro OATH"); }
                    catch (InvalidOperationException error) { MessageBox.Show(form, error.Message, "Cadastro OATH"); }
                    finally { secret.Clear(); }
                }
            };
            var generate = new NeonButton { Text = "Gerar código TOTP", AutoSize = true };
            generate.Click += (sender, e) => {
                try
                {
                    string[] accounts = PicoOath.TotpNames();
                    if (accounts.Length == 0)
                    {
                        MessageBox.Show(form, "Adicione uma conta TOTP primeiro.", "Gerar código");
                        return;
                    }
                    using (var dialog = new Form { Text = "Escolha a conta TOTP", ClientSize = new Size(430, 120), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false })
                    using (var picker = new OptionPicker { Left = 15, Top = 12, Width = 400, AccessibleName = "Conta TOTP" })
                    using (var show = new NeonButton { Left = 15, Top = 55, AutoSize = true, Text = "Mostrar código", DialogResult = DialogResult.OK })
                    {
                        picker.Items.AddRange(accounts);
                        picker.SelectedIndex = 0;
                        dialog.Controls.AddRange(new Control[] { picker, show });
                        dialog.AcceptButton = show; Neon.Style(dialog);
                        if (dialog.ShowDialog(form) == DialogResult.OK)
                            MessageBox.Show(form, PicoOath.CurrentCode((string)picker.SelectedItem), "Código TOTP");
                    }
                }
                catch (InvalidOperationException error) { MessageBox.Show(form, error.Message, "Gerar código"); }
            };
            panel.Controls.AddRange(new Control[] { status, refresh, generate, addTotp, oath, manage, chrome, usb, verify, help });
            var tabs = new TabControl {
                Dock = DockStyle.Fill, DrawMode = TabDrawMode.OwnerDrawFixed,
                SizeMode = TabSizeMode.Fixed, ItemSize = new Size(150, 40)
            };
            tabs.DrawItem += Neon.DrawTab;
            var daily = new TabPage("Chave e contas");
            var device = new TabPage("LED e USB");
            var slots = new TabPage("Slots OTP");
            var advanced = new TabPage("Avançado");
            daily.Controls.Add(panel);
            device.Controls.Add(HardwareViews.Device(form));
            slots.Controls.Add(HardwareViews.Slots(form));
            advanced.Controls.Add(HardwareViews.Advanced(form));
            tabs.TabPages.AddRange(new[] { daily, device, slots, advanced });

            var heading = new Label {
                Dock = DockStyle.Top, Height = 56, Text = "PICO FIDO",
                Padding = new Padding(23, 0, 0, 0), TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Consolas", 24, FontStyle.Bold)
            };
            form.Controls.Add(tabs);
            form.Controls.Add(heading);
            Neon.Style(form);
            heading.ForeColor = Neon.Accent;
            heading.BackColor = Color.FromArgb(16, 33, 23);
            if (!managementSmoke) form.Shown += (sender, e) => update();
            if (managementSmoke)
            {
                using (var timer = new Timer { Interval = 200 })
                {
                    timer.Tick += (sender, e) => {
                        if (tabs.SelectedIndex == tabs.TabCount - 1) form.Close();
                        else tabs.SelectedIndex++;
                    };
                    form.Shown += (sender, e) => timer.Start();
                    Application.Run(form);
                }
            }
            else Application.Run(form);
        }
    }
}
