using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

static class Neon
{
    internal static readonly Color Background = Color.FromArgb(12, 10, 25);
    static readonly Color Surface = Color.FromArgb(27, 21, 48);
    static readonly Color Text = Color.FromArgb(234, 232, 255);
    internal static readonly Color Cyan = Color.FromArgb(82, 249, 237);
    static readonly Color Pink = Color.FromArgb(255, 105, 214);

    internal static void Style(Control root)
    {
        root.BackColor = Background;
        root.ForeColor = Text;
        foreach (Control control in root.Controls) Style(control);
        var button = root as Button;
        if (button != null)
        {
            button.BackColor = Surface;
            button.ForeColor = Cyan;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(49, 132, 146);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(47, 29, 73);
            button.Padding = new Padding(8, 4, 8, 4);
            button.MinimumSize = new Size(0, 34);
        }
        if (root is TextBoxBase || root is ComboBox || root is NumericUpDown)
            root.BackColor = Surface;
    }

    internal static void DrawTab(object sender, DrawItemEventArgs e)
    {
        var tabs = (TabControl)sender;
        bool active = tabs.SelectedIndex == e.Index;
        using (var brush = new SolidBrush(active ? Surface : Background))
            e.Graphics.FillRectangle(brush, e.Bounds);
        if (active)
        {
            using (var pen = new Pen(Pink, 3))
                e.Graphics.DrawLine(pen, e.Bounds.Left + 2, e.Bounds.Bottom - 2,
                    e.Bounds.Right - 2, e.Bounds.Bottom - 2);
        }
        TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, e.Bounds,
            active ? Cyan : Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

static class HardwareViews
{
    static Label Note(string text)
    {
        return new Label { Text = text, MaximumSize = new Size(630, 0), AutoSize = true, Margin = new Padding(3, 6, 3, 4) };
    }

    static Button Action(string text)
    {
        return new Button { Text = text, AutoSize = true, Margin = new Padding(3, 8, 3, 4) };
    }

    static FlowLayoutPanel Section()
    {
        return new FlowLayoutPanel {
            Dock = DockStyle.Fill, Padding = new Padding(20), AutoScroll = true,
            FlowDirection = FlowDirection.TopDown, WrapContents = false
        };
    }

    static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        row.Controls.AddRange(controls);
        return row;
    }

    static void ShowError(IWin32Window owner, Exception error)
    {
        MessageBox.Show(owner, error.GetBaseException().Message, "Configuração da Pico");
    }

    static int Option(SortedDictionary<byte, byte[]> fields, byte tag, int fallback)
    {
        byte[] data;
        if (!fields.TryGetValue(tag, out data) || data.Length < 1 || data.Length > 4) return fallback;
        return data.Aggregate(0, (value, octet) => (value << 8) | octet);
    }

    static void Flag(SortedDictionary<byte, byte[]> data, byte tag, int length, int flag, bool on, int fallback)
    {
        int value = Option(data, tag, fallback);
        value = on ? value | flag : value & ~flag;
        var bytes = new byte[length];
        for (int i = length - 1; i >= 0; i--) { bytes[i] = (byte)value; value >>= 8; }
        data[tag] = bytes;
    }

    internal static Control Device(Form owner)
    {
        var panel = Section();
        var load = Action("Ler ajustes da Pico");
        var save = Action("Aplicar na Pico");
        var state = Note("Clique em Ler ajustes da Pico para começar.");
        var steady = new CheckBox { Text = "Desativar o efeito de piscar", AutoSize = true };
        var dim = new CheckBox { Text = "Ajustar brilho do LED RGB externo", AutoSize = true };
        var brightness = new NumericUpDown { Minimum = 0, Maximum = 15, Width = 80, AccessibleName = "Brilho RGB" };
        var product = new TextBox { MaxLength = 31, Width = 260, AccessibleName = "Nome USB" };
        var vid = new TextBox { MaxLength = 4, Width = 90, AccessibleName = "VID em hexadecimal" };
        var pid = new TextBox { MaxLength = 4, Width = 90, AccessibleName = "PID em hexadecimal" };
        var custom = new CheckBox { Text = "Usar VID e PID próprios", AutoSize = true };
        var keyboard = new CheckBox { Text = "Ativar interface de teclado para OTP", AutoSize = true };
        var curve = new CheckBox { Text = "Ativar curva secp256k1 para FIDO", AutoSize = true };
        var fields = Section();
        fields.Padding = Padding.Empty;
        fields.AutoScroll = false;
        fields.Dock = DockStyle.None;
        fields.AutoSize = true;
        fields.Enabled = false;
        fields.Controls.AddRange(new Control[] {
            Note("LED da placa"), steady, Note("O LED integrado da Pico 2 não tem ajuste de brilho pelo firmware."),
            Row(dim, brightness), Note("Identidade USB"),
            Row(Note("Nome"), product), custom, Row(Note("VID"), vid, Note("PID"), pid),
            Note("Opções avançadas"), keyboard, curve, save
        });
        SortedDictionary<byte, byte[]> original = null;
        Action usbAvailability = () => { vid.Enabled = custom.Checked; pid.Enabled = custom.Checked; };
        custom.CheckedChanged += (sender, e) => usbAvailability();
        dim.CheckedChanged += (sender, e) => brightness.Enabled = dim.Enabled && dim.Checked;
        load.Click += async (sender, e) => {
            load.Enabled = fields.Enabled = false;
            try
            {
                original = await Task.Run((Func<SortedDictionary<byte, byte[]>>)PicoSettings.Configuration);
                int opts = Option(original, 6, 0);
                int driver = original.ContainsKey(12) && original[12].Length > 0 ? original[12][0] : 0;
                steady.Checked = (opts & 8) != 0;
                dim.Enabled = driver == 3;
                dim.Checked = (opts & 2) != 0;
                brightness.Value = Math.Min(15, Option(original, 5, 15));
                brightness.Enabled = dim.Enabled && dim.Checked;
                product.Text = original.ContainsKey(9) ? Encoding.ASCII.GetString(original[9]).TrimEnd('\0') : "";
                int id = Option(original, 0, 0x2E8A10FE);
                vid.Text = ((id >> 16) & 0xFFFF).ToString("X4");
                pid.Text = (id & 0xFFFF).ToString("X4");
                custom.Checked = original.ContainsKey(0);
                usbAvailability();
                keyboard.Checked = (Option(original, 11, 31) & 8) != 0;
                curve.Checked = (Option(original, 10, 0) & 8) != 0;
                state.Text = "Ajustes lidos. As alterações só são gravadas ao aplicar.";
                fields.Enabled = true;
            }
            catch (Exception error) { state.Text = "Não foi possível ler os ajustes."; ShowError(owner, error); }
            finally { load.Enabled = true; }
        };
        save.Click += async (sender, e) => {
            if (original == null) return;
            string name = product.Text.Trim();
            if (name.Any(c => c < 32 || c > 126))
            {
                ShowError(owner, new InvalidOperationException("Use até 31 caracteres ASCII no nome USB."));
                return;
            }
            ushort vendor = 0, item = 0;
            if (custom.Checked && (vid.Text.Length != 4 || pid.Text.Length != 4 ||
                !ushort.TryParse(vid.Text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out vendor) ||
                !ushort.TryParse(pid.Text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out item) ||
                vendor == 0 || vendor == 0xFFFF))
            {
                ShowError(owner, new InvalidOperationException("Informe VID e PID com quatro dígitos hexadecimais e um VID válido."));
                return;
            }
            var updated = new SortedDictionary<byte, byte[]>(original);
            Flag(updated, 6, 2, 8, steady.Checked, 0);
            if (dim.Enabled)
            {
                Flag(updated, 6, 2, 2, dim.Checked, 0);
                if (dim.Checked) updated[5] = new[] { (byte)brightness.Value };
            }
            if (name.Length == 0) updated.Remove(9);
            else updated[9] = Encoding.ASCII.GetBytes(name + "\0");
            if (!custom.Checked) updated.Remove(0);
            else updated[0] = new[] { (byte)(vendor >> 8), (byte)vendor, (byte)(item >> 8), (byte)item };
            if (keyboard.Checked != ((Option(original, 11, 31) & 8) != 0))
                Flag(updated, 11, 1, 8, keyboard.Checked, 31);
            if (curve.Checked != ((Option(original, 10, 0) & 8) != 0))
                Flag(updated, 10, 4, 8, curve.Checked, 0);
            bool identityChanged = Option(updated, 0, 0x2E8A10FE) != Option(original, 0, 0x2E8A10FE);
            string instructions = "Ao confirmar a janela, pressione brevemente BOOTSEL na Pico quando ela pedir presença. Reconecte a placa depois de gravar.";
            if (identityChanged) instructions += "\r\n\r\nTrocar VID/PID pode mudar o driver do Windows. Use apenas identificadores atribuídos a você.";
            if (MessageBox.Show(owner, instructions, "Aplicar configurações", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
            fields.Enabled = load.Enabled = false;
            state.Text = "Aguardando a confirmação na Pico…";
            try
            {
                await Task.Run(() => PicoSettings.Apply(original, updated));
                state.Text = "Gravação confirmada pela Pico. Reconecte a placa e leia os ajustes novamente.";
            }
            catch (Exception error) { state.Text = "Leia novamente para conferir o estado da placa."; ShowError(owner, error); }
            finally { load.Enabled = true; }
        };
        panel.Controls.AddRange(new Control[] { Note("LED, identidade USB e interfaces"), load, state, fields });
        return panel;
    }

    internal static Control Slots(Form owner)
    {
        var panel = Section();
        var load = Action("Ler os quatro slots");
        var selector = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280, AccessibleName = "Slot OTP" };
        var mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280, AccessibleName = "Tipo de slot OTP" };
        mode.Items.AddRange(new object[] { "Apagar slot", "Texto digitado", "HOTP digitado", "HMAC-SHA1 desafio/resposta", "Yubico OTP" });
        mode.SelectedIndex = 1;
        var secretLabel = Note("");
        var secret = new TextBox { UseSystemPasswordChar = true, Width = 280, AccessibleName = "Valor do slot OTP", MaxLength = 56 };
        var access = new TextBox { UseSystemPasswordChar = true, Width = 160, AccessibleName = "Código de acesso do slot", MaxLength = 12 };
        var touch = new CheckBox { Text = "Exigir toque para desafio HMAC", Checked = true, AutoSize = true };
        var save = Action("Salvar alteração do slot");
        var state = Note("Leia os slots para começar.");
        bool[] slots = null;
        Action update = () => {
            string[] hints = {
                "O slot será apagado.", "Texto: até 38 letras minúsculas ou números. A Pico digita no campo em foco.",
                "Segredo: 32 dígitos hexadecimais. O contador inicia em zero.",
                "Segredo: 40 dígitos hexadecimais.", "56 dígitos hexadecimais: ID público (6 bytes), ID privado (6 bytes), AES (16 bytes)."
            };
            secretLabel.Text = hints[mode.SelectedIndex];
            secret.Enabled = mode.SelectedIndex > 0;
            touch.Visible = mode.SelectedIndex == 3;
            save.Enabled = slots != null && selector.SelectedIndex >= 0 &&
                (mode.SelectedIndex != 0 || slots[selector.SelectedIndex]);
        };
        mode.SelectedIndexChanged += (sender, e) => { secret.Clear(); update(); };
        selector.SelectedIndexChanged += (sender, e) => update();
        load.Click += async (sender, e) => {
            load.Enabled = save.Enabled = false;
            slots = null;
            try
            {
                var result = await Task.Run((Func<bool[]>)PicoSettings.Slots);
                int previous = selector.SelectedIndex;
                selector.Items.Clear();
                for (int i = 0; i < result.Length; i++)
                    selector.Items.Add("Slot " + (i + 1) + (result[i] ? " • ocupado" : " • livre"));
                slots = result;
                selector.SelectedIndex = Math.Max(previous, 0);
                state.Text = "Selecione o slot e o tipo.";
            }
            catch (Exception error) { state.Text = "Não foi possível consultar os slots."; ShowError(owner, error); }
            finally { load.Enabled = true; update(); }
        };
        save.Click += async (sender, e) => {
            int slot = selector.SelectedIndex, type = mode.SelectedIndex;
            bool occupied = slots[slot], requireTouch = touch.Checked;
            if (occupied && MessageBox.Show(owner,
                "Esta ação substitui ou apaga o conteúdo do slot " + (slot + 1) + ". Confirme que você possui a chave original ou outro método de acesso.",
                "Alterar slot ocupado", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
            string value = secret.Text, code = access.Text;
            secret.Clear();
            access.Clear();
            load.Enabled = save.Enabled = selector.Enabled = mode.Enabled = false;
            try
            {
                await Task.Run(() => PicoSettings.SetSlot(slot, type, value, code, occupied, requireTouch));
                MessageBox.Show(owner, "A Pico confirmou a alteração do slot.", "Slots OTP");
            }
            catch (Exception error) { ShowError(owner, error); }
            finally
            {
                load.Enabled = selector.Enabled = mode.Enabled = true;
                load.PerformClick();
            }
        };
        panel.Controls.AddRange(new Control[] {
            Note("Slots OTP da Pico"), load, state, selector, mode, secretLabel, secret, touch,
            Note("Código de acesso atual do slot (12 hexadecimais). Deixe vazio se não houver."), access, save
        });
        update();
        return panel;
    }

    internal static Control Advanced(Form owner)
    {
        var panel = Section();
        var status = Note("Leia o estado da chave para ver as opções de segurança.");
        var read = Action("Consultar segurança e memória");
        var sync = Action("Sincronizar relógio com o Windows");
        read.Click += async (sender, e) => {
            read.Enabled = false;
            try { status.Text = await Task.Run((Func<string>)PicoSettings.SecurityStatus); }
            catch (Exception error) { ShowError(owner, error); }
            finally { read.Enabled = true; }
        };
        sync.Click += async (sender, e) => {
            sync.Enabled = false;
            try
            {
                await Task.Run((Action)PicoSettings.SyncClock);
                MessageBox.Show(owner, "Relógio atualizado e confirmado pela placa.", "Relógio da Pico");
            }
            catch (Exception error) { ShowError(owner, error); }
            finally { sync.Enabled = true; }
        };
        panel.Controls.AddRange(new Control[] {
            Note("Provisionamento e diagnóstico"), read, status, sync,
            Note("Secure Boot e Secure Lock aparecem em modo de consulta. Ativá-los exige firmware assinado e um plano de recuperação.")
        });
        return panel;
    }
}
