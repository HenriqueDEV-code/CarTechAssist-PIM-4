using CarTechAssist.Contracts.Common;
using CarTechAssist.Contracts.Enums;
using CarTechAssist.Contracts.Usuarios;
using CarTechAssist.Desktop.WinForms.Helpers;
using CarTechAssist.Desktop.WinForms.Services;

namespace CarTechAssist.Desktop.WinForms.Forms
{
    public partial class UsuariosForm : Form
    {
        private readonly ApiClientService _apiClient;
        private readonly UsuariosService _usuariosService;
        private DataGridView dgvUsuarios = null!;
        private Button btnRefresh = null!;
        private Button btnNovoUsuario = null!;
        private ComboBox cmbTipoUsuarioFilter = null!;
        private Button btnFilter = null!;
        private Label lblTitle = null!;

        public UsuariosForm(ApiClientService apiClient)
        {
            _apiClient = apiClient;
            var session = SessionManager.LoadSession();
            if (session != null && !string.IsNullOrEmpty(session.Token))
            {
                _apiClient.SetAuth(session.Token, session.TenantId, session.UsuarioId);
            }
            _usuariosService = new UsuariosService(_apiClient);
            InitializeComponent();
            LoadUsuarios();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "CarTechAssist - Usuários";
            this.Size = new Size(1400, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(26, 28, 36);
            this.MinimumSize = new Size(1200, 700);
            this.WindowState = FormWindowState.Maximized;

            // Header Panel (Navbar)
            var panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(40, 44, 52),
                Padding = new Padding(30, 15, 30, 15)
            };
            this.Controls.Add(panelHeader);

            // Logo/Brand
            var lblBrand = new Label
            {
                Text = "🚗 CarTechAssist",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.FromArgb(76, 175, 80),
                AutoSize = true,
                Location = new Point(30, 20)
            };
            panelHeader.Controls.Add(lblBrand);

            // Navigation buttons
            var btnNavDashboard = CreateNavButton("📊 Dashboard", lblBrand.Right + 30);
            btnNavDashboard.Click += (s, e) => { var dash = new DashboardForm(_apiClient); dash.Show(); this.Close(); };
            panelHeader.Controls.Add(btnNavDashboard);

            var btnNavChamados = CreateNavButton("🎫 Chamados", btnNavDashboard.Right + 10);
            btnNavChamados.Click += (s, e) => { var chamados = new ChamadosForm(_apiClient); chamados.Show(); this.Close(); };
            panelHeader.Controls.Add(btnNavChamados);

            var btnNavUsuarios = CreateNavButton("👥 Usuários", btnNavChamados.Right + 10);
            btnNavUsuarios.BackColor = Color.FromArgb(76, 175, 80);
            panelHeader.Controls.Add(btnNavUsuarios);

            // User info and logout (right side)
            var session = SessionManager.LoadSession();
            var lblUser = new Label
            {
                Text = $"👤 {session?.NomeCompleto ?? "Usuário"}",
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(this.Width - 200, 25),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            panelHeader.Controls.Add(lblUser);

            var btnLogout = new Button
            {
                Text = "🚪 Sair",
                Size = new Size(80, 35),
                Location = new Point(this.Width - 100, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.Click += (s, e) => { SessionManager.ClearSession(); _apiClient.ClearAuth(); var login = new LoginForm(); login.Show(); this.Close(); };
            panelHeader.Controls.Add(btnLogout);

            // Content Panel
            var panelContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(26, 28, 36),
                Padding = new Padding(30),
                AutoScroll = true
            };
            this.Controls.Add(panelContent);

            // Title Section
            lblTitle = new Label
            {
                Text = "👥 Usuários",
                Font = new Font("Segoe UI", 28, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(30, 20)
            };
            panelContent.Controls.Add(lblTitle);

            // Buttons
            btnNovoUsuario = new Button
            {
                Text = "➕ Novo Usuário",
                Location = new Point(1100, 20),
                Size = new Size(180, 45),
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnNovoUsuario.FlatAppearance.BorderSize = 0;
            btnNovoUsuario.Click += BtnNovoUsuario_Click;
            panelContent.Controls.Add(btnNovoUsuario);

            btnRefresh = new Button
            {
                Text = "🔄 Atualizar",
                Location = new Point(900, 20),
                Size = new Size(150, 45),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += (s, e) => LoadUsuarios();
            panelContent.Controls.Add(btnRefresh);

            // Filtros
            var panelFiltros = new Panel
            {
                Location = new Point(30, 90),
                Size = new Size(1300, 80),
                BackColor = Color.FromArgb(40, 44, 52),
                BorderStyle = BorderStyle.None,
                Padding = new Padding(20)
            };
            panelContent.Controls.Add(panelFiltros);

            var lblTipoUsuario = new Label
            {
                Text = "Tipo de Usuário",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 25)
            };
            panelFiltros.Controls.Add(lblTipoUsuario);

            cmbTipoUsuarioFilter = new ComboBox
            {
                Location = new Point(20, 45),
                Size = new Size(250, 30),
                Font = new Font("Segoe UI", 10),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbTipoUsuarioFilter.Items.AddRange(new[] { "Todos", "Cliente", "Agente", "Admin" });
            cmbTipoUsuarioFilter.SelectedIndex = 0;
            panelFiltros.Controls.Add(cmbTipoUsuarioFilter);

            btnFilter = new Button
            {
                Text = "🔍 Filtrar",
                Location = new Point(280, 45),
                Size = new Size(120, 30),
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnFilter.FlatAppearance.BorderSize = 0;
            btnFilter.Click += (s, e) => LoadUsuarios();
            panelFiltros.Controls.Add(btnFilter);

            // DataGridView
            dgvUsuarios = new DataGridView
            {
                Location = new Point(30, 190),
                Size = new Size(1300, 500),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.FromArgb(40, 44, 52),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false
            };
            StyleDataGridView(dgvUsuarios);
            panelContent.Controls.Add(dgvUsuarios);

            this.ResumeLayout(false);
        }

        private Button CreateNavButton(string text, int left)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(left, 15),
                Size = new Size(150, 40),
                BackColor = Color.FromArgb(40, 44, 52),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => { if (btn.BackColor != Color.FromArgb(76, 175, 80)) btn.BackColor = Color.FromArgb(50, 54, 62); };
            btn.MouseLeave += (s, e) => { if (btn.BackColor != Color.FromArgb(76, 175, 80)) btn.BackColor = Color.FromArgb(40, 44, 52); };
            return btn;
        }

        private void StyleDataGridView(DataGridView dgv)
        {
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 30, 30);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            dgv.EnableHeadersVisualStyles = false;
            dgv.DefaultCellStyle.BackColor = Color.FromArgb(40, 44, 52);
            dgv.DefaultCellStyle.ForeColor = Color.White;
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(76, 175, 80);
            dgv.DefaultCellStyle.SelectionForeColor = Color.White;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(50, 54, 62);
            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 10);
            dgv.DefaultCellStyle.Padding = new Padding(10, 5, 10, 5);
        }

        private async void LoadUsuarios()
        {
            try
            {
                btnRefresh.Enabled = false;
                byte? tipo = null;
                if (cmbTipoUsuarioFilter.SelectedIndex > 0)
                {
                    tipo = (byte)cmbTipoUsuarioFilter.SelectedIndex;
                }

                var resultado = await _usuariosService.ListarAsync(tipo: tipo, page: 1, pageSize: 100);
                if (resultado?.Items != null)
                {
                    dgvUsuarios.DataSource = resultado.Items.Select(u => new
                    {
                        u.UsuarioId,
                        NomeCompleto = u.NomeCompleto,
                        Email = u.Email,
                        TipoUsuario = GetTipoUsuarioNome(u.TipoUsuarioId),
                        Ativo = u.Ativo ? "Sim" : "Não"
                    }).ToList();
                }
                else
                {
                    dgvUsuarios.DataSource = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar usuários: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnRefresh.Enabled = true;
            }
        }

        private string GetTipoUsuarioNome(byte tipoUsuarioId)
        {
            return tipoUsuarioId switch
            {
                1 => "Cliente",
                2 => "Agente",
                3 => "Admin",
                _ => "Desconhecido"
            };
        }

        private void BtnNovoUsuario_Click(object? sender, EventArgs e)
        {
            MessageBox.Show("Funcionalidade de criar novo usuário ainda não implementada.", "Informação", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
