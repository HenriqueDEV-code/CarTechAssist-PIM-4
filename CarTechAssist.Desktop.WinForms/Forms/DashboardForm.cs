using CarTechAssist.Contracts.Enums;
using CarTechAssist.Contracts.Tickets;
using CarTechAssist.Desktop.WinForms.Helpers;
using CarTechAssist.Desktop.WinForms.Services;

namespace CarTechAssist.Desktop.WinForms.Forms
{
    public partial class DashboardForm : Form
    {
        private readonly ApiClientService _apiClient;
        private readonly ChamadosService _chamadosService = null!;
        private Panel panelContent = null!;
        private Label lblWelcome = null!;
        
        // Estatísticas
        private Label lblTotalAbertos = null!;
        private Label lblTotalEmAndamento = null!;
        private Label lblTotalResolvidos = null!;
        private Label lblTotalChamados = null!;
        
        // Tabela de chamados recentes
        private DataGridView dgvChamadosRecentes = null!;
        
        // Informações do usuário
        private Label lblNomeUsuario = null!;
        private Label lblTenantId = null!;
        private Label lblUsuarioId = null!;
        private Button btnNovoChamado = null!;
        private Button btnVerTodosChamados = null!;
        private Panel panelChatBot = null!;

        private byte _tipoUsuarioId;
        private bool _isCliente;

        public DashboardForm(ApiClientService apiClient)
        {
            _apiClient = apiClient;
            
            // SEMPRE carregar e configurar autenticação ANTES de qualquer coisa
            var session = SessionManager.LoadSession();
            if (session != null && !string.IsNullOrEmpty(session.Token) && session.TenantId > 0 && session.UsuarioId > 0)
            {
                System.Diagnostics.Debug.WriteLine($"✅ DashboardForm - Sessão encontrada: Token={(!string.IsNullOrEmpty(session.Token) ? "OK" : "NULL")}, TenantId={session.TenantId}, UsuarioId={session.UsuarioId}");
                _apiClient.SetAuth(session.Token, session.TenantId, session.UsuarioId);
                _tipoUsuarioId = session.TipoUsuarioId;
                _isCliente = session.TipoUsuarioId == 1;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"❌ DashboardForm - Sessão NÃO encontrada ou token vazio!");
                System.Diagnostics.Debug.WriteLine($"❌ Session: {session?.Token ?? "NULL"}, TenantId: {session?.TenantId ?? 0}, UsuarioId: {session?.UsuarioId ?? 0}");
                throw new InvalidOperationException("Sessão inválida. Por favor, faça login novamente.");
            }
            
            System.Diagnostics.Debug.WriteLine($"🔍 DashboardForm - Inicializando componentes...");
            _chamadosService = new ChamadosService(_apiClient);
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine($"✅ DashboardForm - Componentes inicializados");
            
            LoadUserInfo();
            LoadDashboardData();
            System.Diagnostics.Debug.WriteLine($"✅ DashboardForm - Construção concluída com sucesso");
        }

        private async void LoadDashboardData()
        {
            try
            {
                // Garantir autenticação antes de fazer a requisição
                var session = SessionManager.LoadSession();
                if (session != null && !string.IsNullOrEmpty(session.Token))
                {
                    _apiClient.SetAuth(session.Token, session.TenantId, session.UsuarioId);
                }

                var resultado = await _chamadosService.ListarAsync(page: 1, pageSize: 1000);
                if (resultado?.Items != null)
                {
                    var todosChamados = resultado.Items.ToList();
                    var totalChamados = resultado.Total;
                    var totalAbertos = todosChamados.Count(c => 
                        c.StatusNome == "1" || 
                        c.StatusNome?.ToLower().Contains("aberto") == true ||
                        c.StatusNome?.ToLower() == "pendente");
                    var totalEmAndamento = todosChamados.Count(c => 
                        c.StatusNome == "2" || 
                        c.StatusNome?.ToLower().Contains("andamento") == true ||
                        c.StatusNome?.ToLower().Contains("em andamento") == true);
                    var totalResolvidos = todosChamados.Count(c => 
                        c.StatusNome == "3" || 
                        c.StatusNome?.ToLower().Contains("resolvido") == true);
                    var chamadosRecentes = todosChamados.OrderByDescending(c => c.DataCriacao).Take(10).ToList();

                    if (InvokeRequired)
                    {
                        Invoke(() => UpdateDashboardStats(totalChamados, totalAbertos, totalEmAndamento, totalResolvidos, chamadosRecentes));
                    }
                    else
                    {
                        UpdateDashboardStats(totalChamados, totalAbertos, totalEmAndamento, totalResolvidos, chamadosRecentes);
                    }
                }
            }
            catch (HttpRequestException httpEx)
            {
                System.Diagnostics.Debug.WriteLine($"❌ DashboardForm.LoadDashboardData - HttpRequestException: {httpEx.Message}");
                if (httpEx.Data.Contains("StatusCode") && httpEx.Data["StatusCode"] is System.Net.HttpStatusCode statusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ StatusCode: {statusCode}");
                    if (statusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        // Verificar se a sessão ainda está válida
                        var session = SessionManager.LoadSession();
                        System.Diagnostics.Debug.WriteLine($"🔍 Sessão atual: Token={(!string.IsNullOrEmpty(session?.Token) ? "OK" : "NULL")}, TenantId={session?.TenantId ?? 0}, UsuarioId={session?.UsuarioId ?? 0}");
                        
                        if (InvokeRequired)
                        {
                            Invoke(() => MessageBox.Show($"Erro de autenticação ao carregar chamados. Por favor, faça login novamente.\n\nDetalhes: {httpEx.Message}", "Erro de Autenticação", MessageBoxButtons.OK, MessageBoxIcon.Error));
                        }
                        else
                        {
                            MessageBox.Show($"Erro de autenticação ao carregar chamados. Por favor, faça login novamente.\n\nDetalhes: {httpEx.Message}", "Erro de Autenticação", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        if (InvokeRequired)
                        {
                            Invoke(() => MessageBox.Show($"Erro ao carregar dashboard: {httpEx.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error));
                        }
                        else
                        {
                            MessageBox.Show($"Erro ao carregar dashboard: {httpEx.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
                else
                {
                    if (InvokeRequired)
                    {
                        Invoke(() => MessageBox.Show($"Erro ao carregar dashboard: {httpEx.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error));
                    }
                    else
                    {
                        MessageBox.Show($"Erro ao carregar dashboard: {httpEx.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ DashboardForm.LoadDashboardData - Exception: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ StackTrace: {ex.StackTrace}");
                if (InvokeRequired)
                {
                    Invoke(() => MessageBox.Show($"Erro ao carregar dashboard: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error));
                }
                else
                {
                    MessageBox.Show($"Erro ao carregar dashboard: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void UpdateDashboardStats(int total, int abertos, int emAndamento, int resolvidos, List<TicketView> chamadosRecentes)
        {
            lblTotalAbertos.Text = abertos.ToString();
            lblTotalEmAndamento.Text = emAndamento.ToString();
            lblTotalResolvidos.Text = resolvidos.ToString();
            lblTotalChamados.Text = total.ToString();

            if (chamadosRecentes.Any())
            {
                dgvChamadosRecentes.DataSource = chamadosRecentes.Select(c => new
                {
                    c.ChamadoId,
                    Numero = c.Numero,
                    Titulo = c.Titulo,
                    Status = GetStatusNome(c.StatusNome),
                    Prioridade = c.PrioridadeNome,
                    DataCriacao = c.DataCriacao.ToString("dd/MM/yyyy HH:mm")
                }).ToList();
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

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "CarTechAssist - Dashboard";
            this.Size = new Size(1600, 900);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(26, 28, 36);
            this.MinimumSize = new Size(1400, 800);
            this.WindowState = FormWindowState.Maximized;

            // Header Panel (Navbar)
            var panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(40, 44, 52),
                Padding = new Padding(30, 10, 30, 10)
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
            btnNavDashboard.BackColor = Color.FromArgb(76, 175, 80);
            panelHeader.Controls.Add(btnNavDashboard);

            var btnNavChamados = CreateNavButton("🎫 Chamados", btnNavDashboard.Right + 10);
            btnNavChamados.Click += (s, e) =>
            {
                NavigationGuard.Begin(NavigationReason.SwitchForm);
                try
                {
                    var chamados = new ChamadosForm(_apiClient);
                    chamados.Show();
                    this.Close();
                }
                catch
                {
                    NavigationGuard.Reset();
                    throw;
                }
            };
            panelHeader.Controls.Add(btnNavChamados);

            var btnNavNovo = CreateNavButton("➕ Novo Chamado", btnNavChamados.Right + 10);
            btnNavNovo.Click += (s, e) => { var criar = new CriarChamadoForm(_apiClient); criar.ShowDialog(); LoadDashboardData(); };
            panelHeader.Controls.Add(btnNavNovo);

            // Botão Usuários (apenas para Admin)
            if (_tipoUsuarioId == 3)
            {
                var btnNavUsuarios = CreateNavButton("👥 Usuários", btnNavNovo.Right + 10);
                btnNavUsuarios.Click += (s, e) =>
                {
                    NavigationGuard.Begin(NavigationReason.SwitchForm);
                    try
                    {
                        var usuarios = new UsuariosForm(_apiClient);
                        usuarios.Show();
                        this.Close();
                    }
                    catch
                    {
                        NavigationGuard.Reset();
                        throw;
                    }
                };
                panelHeader.Controls.Add(btnNavUsuarios);
            }

            // User info and logout (right side)
            var session = SessionManager.LoadSession();
            var lblUser = new Label
            {
                Text = $"👤 {session?.NomeCompleto ?? "Usuário"}",
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(this.Width - 200, 20),
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
            panelContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(26, 28, 36),
                Padding = new Padding(30, 50, 30, 30),
                AutoScroll = true
            };
            this.Controls.Add(panelContent);

            // Título
            lblWelcome = new Label
            {
                Text = "📊 Dashboard",
                Font = new Font("Segoe UI", 32, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(30, 10)
            };
            panelContent.Controls.Add(lblWelcome);

            var lblSubtitle = new Label
            {
                Text = "Bem-vindo ao CarTechAssist",
                Font = new Font("Segoe UI", 14),
                ForeColor = Color.FromArgb(150, 150, 150),
                AutoSize = true,
                Location = new Point(30, 60)
            };
            panelContent.Controls.Add(lblSubtitle);

            // Cards de Estatísticas (4 cards em linha - igual à web)
            int cardY = 120;
            int cardWidth = 280;
            int cardHeight = 120;
            int cardSpacing = 20;

            // Card Abertos
            var panelAbertos = CreateStatCard("Chamados Abertos", "0", Color.FromArgb(76, 175, 80), cardY, cardWidth, cardHeight);
            lblTotalAbertos = (Label)panelAbertos.Controls[1];
            panelContent.Controls.Add(panelAbertos);

            // Card Em Andamento
            var panelEmAndamento = CreateStatCard("Em Andamento", "0", Color.FromArgb(255, 152, 0), cardY, cardWidth, cardHeight);
            panelEmAndamento.Location = new Point(panelAbertos.Right + cardSpacing, cardY);
            lblTotalEmAndamento = (Label)panelEmAndamento.Controls[1];
            panelContent.Controls.Add(panelEmAndamento);

            // Card Resolvidos
            var panelResolvidos = CreateStatCard("Resolvidos", "0", Color.FromArgb(76, 175, 80), cardY, cardWidth, cardHeight);
            panelResolvidos.Location = new Point(panelEmAndamento.Right + cardSpacing, cardY);
            lblTotalResolvidos = (Label)panelResolvidos.Controls[1];
            panelContent.Controls.Add(panelResolvidos);

            // Card Total
            var panelTotal = CreateStatCard("Total de Chamados", "0", Color.FromArgb(33, 150, 243), cardY, cardWidth, cardHeight);
            panelTotal.Location = new Point(panelResolvidos.Right + cardSpacing, cardY);
            lblTotalChamados = (Label)panelTotal.Controls[1];
            panelContent.Controls.Add(panelTotal);

            // Layout igual à web: 8 colunas para tabela, 4 colunas para painel de usuário
            int tableY = cardY + cardHeight + 40;
            
            // Tabela de Chamados Recentes (esquerda - 8 colunas)
            var lblChamadosRecentes = new Label
            {
                Text = "🕐 Chamados Recentes",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(30, tableY)
            };
            panelContent.Controls.Add(lblChamadosRecentes);

            // Painel para a tabela (card style)
            var panelTable = new Panel
            {
                Location = new Point(30, tableY + 40),
                Size = new Size(800, 400),
                BackColor = Color.FromArgb(40, 44, 52),
                BorderStyle = BorderStyle.None,
                Padding = new Padding(20)
            };
            panelContent.Controls.Add(panelTable);

            dgvChamadosRecentes = new DataGridView
            {
                Dock = DockStyle.Fill,
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
            StyleDataGridView(dgvChamadosRecentes);
            dgvChamadosRecentes.CellDoubleClick += DgvChamadosRecentes_CellDoubleClick;
            panelTable.Controls.Add(dgvChamadosRecentes);

            // Painel de Informações do Usuário (direita - 4 colunas)
            int userPanelX = panelTable.Right + 30;
            var panelUserInfo = new Panel
            {
                Location = new Point(userPanelX, tableY + 40),
                Size = new Size(380, 400),
                BackColor = Color.FromArgb(40, 44, 52),
                BorderStyle = BorderStyle.None,
                Padding = new Padding(25)
            };
            panelContent.Controls.Add(panelUserInfo);

            var lblUserInfoTitle = new Label
            {
                Text = "👤 Informações do Usuário",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(25, 25)
            };
            panelUserInfo.Controls.Add(lblUserInfoTitle);

            int infoY = 70;
            var lblNomeLabel = new Label
            {
                Text = "Nome:",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(150, 150, 150),
                AutoSize = true,
                Location = new Point(25, infoY)
            };
            panelUserInfo.Controls.Add(lblNomeLabel);

            lblNomeUsuario = new Label
            {
                Text = "-",
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(90, infoY)
            };
            panelUserInfo.Controls.Add(lblNomeUsuario);
            infoY += 35;

            var lblTenantLabel = new Label
            {
                Text = "Tenant ID:",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(150, 150, 150),
                AutoSize = true,
                Location = new Point(25, infoY)
            };
            panelUserInfo.Controls.Add(lblTenantLabel);

            lblTenantId = new Label
            {
                Text = "-",
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(120, infoY)
            };
            panelUserInfo.Controls.Add(lblTenantId);
            infoY += 35;

            var lblUsuarioLabel = new Label
            {
                Text = "Usuário ID:",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(150, 150, 150),
                AutoSize = true,
                Location = new Point(25, infoY)
            };
            panelUserInfo.Controls.Add(lblUsuarioLabel);

            lblUsuarioId = new Label
            {
                Text = "-",
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(120, infoY)
            };
            panelUserInfo.Controls.Add(lblUsuarioId);
            infoY += 50;

            // Linha divisória
            var divider = new Panel
            {
                Location = new Point(25, infoY),
                Size = new Size(330, 1),
                BackColor = Color.FromArgb(60, 60, 60)
            };
            panelUserInfo.Controls.Add(divider);
            infoY += 20;

            btnNovoChamado = new Button
            {
                Text = "➕ Novo Chamado",
                Location = new Point(25, infoY),
                Size = new Size(330, 45),
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnNovoChamado.FlatAppearance.BorderSize = 0;
            btnNovoChamado.Click += BtnNovoChamado_Click;
            panelUserInfo.Controls.Add(btnNovoChamado);
            infoY += 55;

            btnVerTodosChamados = new Button
            {
                Text = "📋 Ver Todos os Chamados",
                Location = new Point(25, infoY),
                Size = new Size(330, 45),
                BackColor = Color.FromArgb(40, 44, 52),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12),
                Cursor = Cursors.Hand
            };
            btnVerTodosChamados.FlatAppearance.BorderSize = 1;
            btnVerTodosChamados.FlatAppearance.BorderColor = Color.FromArgb(76, 175, 80);
            btnVerTodosChamados.Click += BtnVerTodosChamados_Click;
            panelUserInfo.Controls.Add(btnVerTodosChamados);

            // Painel de dica do ChatBot (apenas para Cliente)
            if (_isCliente)
            {
                infoY += 60;
                panelChatBot = new Panel
                {
                    Location = new Point(25, infoY),
                    Size = new Size(330, 150),
                    BackColor = Color.FromArgb(30, 40, 50),
                    BorderStyle = BorderStyle.FixedSingle,
                    Padding = new Padding(15, 15, 15, 15),
                    AutoScroll = true
                };
                panelUserInfo.Controls.Add(panelChatBot);

                var lblChatBotTitle = new Label
                {
                    Text = "🤖 Como Acessar o ChatBot IA",
                    Font = new Font("Segoe UI", 13, FontStyle.Bold),
                    ForeColor = Color.FromArgb(76, 175, 80),
                    AutoSize = true,
                    Location = new Point(15, 15)
                };
                panelChatBot.Controls.Add(lblChatBotTitle);

                var lblChatBotText = new Label
                {
                    Text = "Ao criar um chamado ou enviar uma mensagem, nossa IA irá analisar automaticamente e tentar resolver seu problema. Acesse qualquer chamado para iniciar a conversa com o assistente virtual!",
                    Font = new Font("Segoe UI", 10),
                    ForeColor = Color.FromArgb(200, 200, 200),
                    AutoSize = false,
                    Size = new Size(300, 100),
                    Location = new Point(15, 50)
                };
                panelChatBot.Controls.Add(lblChatBotText);
            }

            this.ResumeLayout(false);
        }

        private Panel CreateStatCard(string title, string value, Color accentColor, int y, int width, int height)
        {
            var panel = new Panel
            {
                Size = new Size(width, height),
                BackColor = Color.FromArgb(40, 44, 52),
                BorderStyle = BorderStyle.None,
                Location = new Point(30, y),
                Padding = new Padding(25)
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 12),
                ForeColor = Color.FromArgb(150, 150, 150),
                AutoSize = true,
                Location = new Point(25, 25)
            };
            panel.Controls.Add(lblTitle);

            var lblValue = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 42, FontStyle.Bold),
                ForeColor = accentColor,
                AutoSize = true,
                Location = new Point(25, 50)
            };
            panel.Controls.Add(lblValue);

            // Ícone
            var lblIcon = new Label
            {
                Text = "📊",
                Font = new Font("Segoe UI", 36),
                ForeColor = accentColor,
                AutoSize = true,
                Location = new Point(width - 80, 25),
                TextAlign = ContentAlignment.MiddleRight
            };
            panel.Controls.Add(lblIcon);

            return panel;
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

        private void LoadUserInfo()
        {
            var session = SessionManager.LoadSession();
            if (session != null)
            {
                lblNomeUsuario.Text = session.NomeCompleto ?? "Usuário";
                lblTenantId.Text = session.TenantId.ToString();
                lblUsuarioId.Text = session.UsuarioId.ToString();
            }
        }

        private void BtnNovoChamado_Click(object? sender, EventArgs e)
        {
            var session = SessionManager.LoadSession();
            if (session != null && !string.IsNullOrEmpty(session.Token))
            {
                _apiClient.SetAuth(session.Token, session.TenantId, session.UsuarioId);
            }
            var criarForm = new CriarChamadoForm(_apiClient);
            criarForm.ShowDialog();
            LoadDashboardData();
        }

        private void BtnVerTodosChamados_Click(object? sender, EventArgs e)
        {
            var session = SessionManager.LoadSession();
            if (session != null && !string.IsNullOrEmpty(session.Token))
            {
                _apiClient.SetAuth(session.Token, session.TenantId, session.UsuarioId);
            }
            NavigationGuard.Begin(NavigationReason.SwitchForm);
            try
            {
                var chamadosForm = new ChamadosForm(_apiClient);
                chamadosForm.Show();
                this.Close();
            }
            catch
            {
                NavigationGuard.Reset();
                throw;
            }
        }

        private void DgvChamadosRecentes_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && dgvChamadosRecentes.Rows[e.RowIndex].DataBoundItem != null)
            {
                var row = dgvChamadosRecentes.Rows[e.RowIndex];
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
    }
}
