using CarTechAssist.Contracts.Tickets;
using CarTechAssist.Contracts.Enums;
using CarTechAssist.Desktop.WinForms.Helpers;
using CarTechAssist.Desktop.WinForms.Services;

namespace CarTechAssist.Desktop.WinForms.Forms
{
    public partial class ChamadosForm : Form
    {
        private readonly ApiClientService _apiClient;
        private readonly ChamadosService _chamadosService;
        private DataGridView dgvChamados = null!;
        private Button btnNovo = null!;
        private Button btnRefresh = null!;
        private Label lblTitle = null!;
        private ComboBox cmbStatusFiltro = null!;
        private Button btnFiltrar = null!;
        private Button btnLimpar = null!;
        private Panel panelFiltros = null!;
        private byte? _statusFiltro = null;

        public ChamadosForm(ApiClientService apiClient)
        {
            _apiClient = apiClient;
            var session = SessionManager.LoadSession();
            if (session != null && !string.IsNullOrEmpty(session.Token))
            {
                _apiClient.SetAuth(session.Token, session.TenantId, session.UsuarioId);
            }
            _chamadosService = new ChamadosService(_apiClient);
            InitializeComponent();
            LoadChamados();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "CarTechAssist - Chamados";
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
            btnNavDashboard.Click += (s, e) =>
            {
                NavigationGuard.Begin(NavigationReason.SwitchForm);
                try
                {
                    var dash = new DashboardForm(_apiClient);
                    dash.Show();
                    this.Close();
                }
                catch
                {
                    NavigationGuard.Reset();
                    throw;
                }
            };
            panelHeader.Controls.Add(btnNavDashboard);

            var btnNavChamados = CreateNavButton("🎫 Chamados", btnNavDashboard.Right + 10);
            btnNavChamados.BackColor = Color.FromArgb(76, 175, 80);
            panelHeader.Controls.Add(btnNavChamados);

            var btnNavNovo = CreateNavButton("➕ Novo Chamado", btnNavChamados.Right + 10);
            btnNavNovo.Click += (s, e) => { var criar = new CriarChamadoForm(_apiClient); criar.ShowDialog(); LoadChamados(); };
            panelHeader.Controls.Add(btnNavNovo);

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
                Text = "Logout",
                Size = new Size(100, 35),
                Location = new Point(this.Width - 120, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.Click += (s, e) =>
            {
                SessionManager.ClearSession();
                _apiClient.ClearAuth();
                NavigationGuard.Begin(NavigationReason.Logout);
                this.Close();
            };
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
                Text = "🎫 Chamados",
                Font = new Font("Segoe UI", 28, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(30, 20)
            };
            panelContent.Controls.Add(lblTitle);

            var lblSubtitle = new Label
            {
                Text = "Gerencie seus chamados de suporte",
                Font = new Font("Segoe UI", 12),
                ForeColor = Color.FromArgb(150, 150, 150),
                AutoSize = true,
                Location = new Point(30, 60)
            };
            panelContent.Controls.Add(lblSubtitle);

            btnNovo = new Button
            {
                Text = "➕ Novo Chamado",
                Location = new Point(1100, 20),
                Size = new Size(180, 45),
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnNovo.FlatAppearance.BorderSize = 0;
            btnNovo.Click += BtnNovo_Click;
            panelContent.Controls.Add(btnNovo);

            // Filtros Panel
            panelFiltros = new Panel
            {
                Location = new Point(30, 110),
                Size = new Size(1300, 120),
                BackColor = Color.FromArgb(40, 44, 52),
                BorderStyle = BorderStyle.None,
                Padding = new Padding(20)
            };
            panelContent.Controls.Add(panelFiltros);

            var lblFiltrosTitle = new Label
            {
                Text = "Filtros",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 15)
            };
            panelFiltros.Controls.Add(lblFiltrosTitle);

            btnLimpar = new Button
            {
                Text = "Limpar",
                Location = new Point(1200, 15),
                Size = new Size(80, 30),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9),
                Cursor = Cursors.Hand
            };
            btnLimpar.FlatAppearance.BorderSize = 0;
            btnLimpar.Click += BtnLimpar_Click;
            panelFiltros.Controls.Add(btnLimpar);

            var lblStatus = new Label
            {
                Text = "Status",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 55)
            };
            panelFiltros.Controls.Add(lblStatus);

            cmbStatusFiltro = new ComboBox
            {
                Location = new Point(20, 75),
                Size = new Size(250, 30),
                Font = new Font("Segoe UI", 10),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbStatusFiltro.Items.AddRange(new[] { "Todos", "Aberto", "Em Andamento", "Resolvido", "Cancelado" });
            cmbStatusFiltro.SelectedIndex = 0;
            panelFiltros.Controls.Add(cmbStatusFiltro);

            btnFiltrar = new Button
            {
                Text = "🔍 Filtrar",
                Location = new Point(280, 75),
                Size = new Size(120, 30),
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnFiltrar.FlatAppearance.BorderSize = 0;
            btnFiltrar.Click += BtnFiltrar_Click;
            panelFiltros.Controls.Add(btnFiltrar);

            // DataGridView
            dgvChamados = new DataGridView
            {
                Location = new Point(30, 250),
                Size = new Size(1300, 450),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.FromArgb(32, 34, 44),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                EnableHeadersVisualStyles = false
            };
            StyleDataGridView(dgvChamados);
            dgvChamados.CellDoubleClick += DgvChamados_CellDoubleClick;
            panelContent.Controls.Add(dgvChamados);

            // Botão Refresh (inicializado ANTES de LoadChamados)
            btnRefresh = new Button
            {
                Text = "🔄 Atualizar",
                Location = new Point(30, 710),
                Size = new Size(120, 40),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += (s, e) => LoadChamados();
            panelContent.Controls.Add(btnRefresh);

            this.ResumeLayout(false);
        }

        private Button CreateNavButton(string text, int left)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(left, 20),
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
            dgv.EnableHeadersVisualStyles = false;
            dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgv.ColumnHeadersHeight = 42;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 32, 40);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(30, 32, 40);
            dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 11, FontStyle.Bold);

            dgv.DefaultCellStyle.BackColor = Color.FromArgb(44, 48, 60);
            dgv.DefaultCellStyle.ForeColor = Color.White;
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(76, 175, 80);
            dgv.DefaultCellStyle.SelectionForeColor = Color.White;
            dgv.DefaultCellStyle.Padding = new Padding(14, 6, 14, 6);
            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 10);

            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(52, 56, 70);

            dgv.GridColor = Color.FromArgb(60, 64, 75);
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.RowTemplate.Height = 40;
        }

        private async void LoadChamados()
        {
            try
            {
                if (btnRefresh != null) btnRefresh.Enabled = false;
                var result = await _chamadosService.ListarAsync(statusId: _statusFiltro, page: 1, pageSize: 100);

                if (result?.Items != null)
                {
                    dgvChamados.DataSource = result.Items.Select(c => new
                    {
                        c.ChamadoId,
                        Numero = c.Numero,
                        Titulo = c.Titulo,
                        Status = GetStatusNome(c.StatusNome),
                        Prioridade = c.PrioridadeNome,
                        Canal = c.CanalNome,
                        DataCriacao = c.DataCriacao.ToString("dd/MM/yyyy HH:mm")
                    }).ToList();
                }
                else
                {
                    dgvChamados.DataSource = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar chamados: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (btnRefresh != null) btnRefresh.Enabled = true;
            }
        }

        private string GetStatusNome(string? statusNome)
        {
            if (string.IsNullOrEmpty(statusNome))
                return "Desconhecido";

            return statusNome switch
            {
                "1" => "Aberto",
                "2" => "Em Andamento",
                "3" => "Resolvido",
                "4" => "Fechado",
                "5" => "Cancelado",
                _ => statusNome
            };
        }

        private void BtnFiltrar_Click(object? sender, EventArgs e)
        {
            _statusFiltro = cmbStatusFiltro.SelectedIndex switch
            {
                0 => null, // Todos
                1 => 1,    // Aberto
                2 => 2,    // Em Andamento
                3 => 3,    // Resolvido
                4 => 4,    // Cancelado
                _ => null
            };
            LoadChamados();
        }

        private void BtnLimpar_Click(object? sender, EventArgs e)
        {
            cmbStatusFiltro.SelectedIndex = 0;
            _statusFiltro = null;
            LoadChamados();
        }

        private void DgvChamados_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && dgvChamados.Rows[e.RowIndex].DataBoundItem != null)
            {
                var row = dgvChamados.Rows[e.RowIndex];
                var chamadoId = Convert.ToInt64(row.Cells["ChamadoId"].Value);
                var session = SessionManager.LoadSession();
                if (session != null && !string.IsNullOrEmpty(session.Token))
                {
                    _apiClient.SetAuth(session.Token, session.TenantId, session.UsuarioId);
                }
                var detalhesForm = new ChamadoDetalhesForm(_apiClient, chamadoId);
                detalhesForm.Show();
                this.Hide();
            }
        }

        private void BtnNovo_Click(object? sender, EventArgs e)
        {
            var session = SessionManager.LoadSession();
            if (session != null && !string.IsNullOrEmpty(session.Token))
            {
                _apiClient.SetAuth(session.Token, session.TenantId, session.UsuarioId);
            }
            var criarForm = new CriarChamadoForm(_apiClient);
            criarForm.ShowDialog();
            LoadChamados();
        }
    }
}
