using CarTechAssist.Contracts.Auth;
using CarTechAssist.Desktop.WinForms.Helpers;
using CarTechAssist.Desktop.WinForms.Services;

namespace CarTechAssist.Desktop.WinForms.Forms
{
    public partial class LoginForm : Form
    {
        private readonly ApiClientService _apiClient;
        private readonly AuthService _authService;
        private Panel panelLeft = null!;
        private Panel panelRight = null!;
        private TextBox txtLogin = null!;
        private TextBox txtSenha = null!;
        private Button btnLogin = null!;
        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private Label lblError = null!;
        private LinkLabel lnkForgotPassword = null!;
        private LinkLabel lnkRegister = null!;

        // Inicializador do Formulário de Login
        public LoginForm()
        {
            _apiClient = new ApiClientService();
            _authService = new AuthService(_apiClient);
            InitializeComponent();
            ApplyDarkTheme();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form properties
            this.Text = "CarTechAssist - Login";
            this.Size = new Size(1000, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(26, 26, 26);
            this.MinimumSize = new Size(800, 500);

            // Left Panel (Visual)
            panelLeft = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            this.Controls.Add(panelLeft);

            // Branding on left
            lblTitle = new Label
            {
                Text = "CAR TECH ASSIST",
                Font = new Font("Segoe UI", 32, FontStyle.Bold),
                ForeColor = Color.FromArgb(76, 175, 80),
                AutoSize = true,
                Location = new Point(50, 200)
            };
            panelLeft.Controls.Add(lblTitle);

            lblSubtitle = new Label
            {
                Text = "Sistema de Gerenciamento de Chamados",
                Font = new Font("Segoe UI", 12),
                ForeColor = Color.FromArgb(200, 200, 200),
                AutoSize = true,
                Location = new Point(50, 280)
            };
            panelLeft.Controls.Add(lblSubtitle);

            // Right Panel (Form)
            panelRight = new Panel
            {
                Dock = DockStyle.Right,
                Width = 450,
                BackColor = Color.FromArgb(40, 44, 52),
                Padding = new Padding(50)
            };
            this.Controls.Add(panelRight);

            // Login Form
            var lblFormTitle = new Label
            {
                Text = "Login",
                Font = new Font("Segoe UI", 28, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(50, 50)
            };
            panelRight.Controls.Add(lblFormTitle);

            // Error Label
            lblError = new Label
            {
                Text = "",
                ForeColor = Color.FromArgb(255, 107, 107),
                AutoSize = true,
                Location = new Point(50, 120),
                Visible = false
            };
            panelRight.Controls.Add(lblError);

            // Username
            var lblUser = new Label
            {
                Text = "User Name",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(199, 199, 199),
                AutoSize = true,
                Location = new Point(50, 160)
            };
            panelRight.Controls.Add(lblUser);

            txtLogin = new TextBox
            {
                Location = new Point(50, 185),
                Size = new Size(350, 35),
                BackColor = Color.FromArgb(40, 44, 52),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 11)
            };
            txtLogin.SetPlaceholder("Enter username");
            panelRight.Controls.Add(txtLogin);
            AddUnderline(txtLogin);

            // Password
            var lblPass = new Label
            {
                Text = "Password",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(199, 199, 199),
                AutoSize = true,
                Location = new Point(50, 250)
            };
            panelRight.Controls.Add(lblPass);

            txtSenha = new TextBox
            {
                Location = new Point(50, 275),
                Size = new Size(350, 35),
                BackColor = Color.FromArgb(40, 44, 52),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 11),
                UseSystemPasswordChar = true
            };
            txtSenha.SetPlaceholder("Password");
            panelRight.Controls.Add(txtSenha);
            AddUnderline(txtSenha);

            // Forgot Password
            lnkForgotPassword = new LinkLabel
            {
                Text = "Forgot password",
                Location = new Point(50, 330),
                AutoSize = true,
                LinkColor = Color.FromArgb(76, 175, 80),
                ActiveLinkColor = Color.FromArgb(69, 160, 73),
                VisitedLinkColor = Color.FromArgb(76, 175, 80)
            };
            panelRight.Controls.Add(lnkForgotPassword);

            // Login Button
            btnLogin = new Button
            {
                Text = "Login",
                Location = new Point(50, 370),
                Size = new Size(350, 45),
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.Click += BtnLogin_Click;
            btnLogin.MouseEnter += (s, e) => btnLogin.BackColor = Color.FromArgb(69, 160, 73);
            btnLogin.MouseLeave += (s, e) => btnLogin.BackColor = Color.FromArgb(76, 175, 80);
            panelRight.Controls.Add(btnLogin);

            // Register Link
            lnkRegister = new LinkLabel
            {
                Text = "Create new account",
                Location = new Point(50, 440),
                AutoSize = true,
                LinkColor = Color.FromArgb(176, 176, 176),
                ActiveLinkColor = Color.White,
                VisitedLinkColor = Color.FromArgb(176, 176, 176)
            };
            panelRight.Controls.Add(lnkRegister);

            // Enter key to login
            txtSenha.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) BtnLogin_Click(btnLogin, EventArgs.Empty); };
            txtLogin.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) txtSenha.Focus(); };

            this.ResumeLayout(false);
        }

        private void AddUnderline(TextBox textBox)
        {
            var panel = new Panel
            {
                Location = new Point(textBox.Left, textBox.Bottom),
                Size = new Size(textBox.Width, 1),
                BackColor = Color.FromArgb(74, 74, 74)
            };
            panelRight.Controls.Add(panel);
            panel.BringToFront();

            textBox.Enter += (s, e) => panel.BackColor = Color.FromArgb(76, 175, 80);
            textBox.Leave += (s, e) => panel.BackColor = Color.FromArgb(74, 74, 74);
        }

        private void ApplyDarkTheme()
        {
            // Additional dark theme styling if needed
        }

        private async void BtnLogin_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtLogin.Text) || string.IsNullOrWhiteSpace(txtSenha.Text))
            {
                ShowError("Por favor, preencha todos os campos.");
                return;
            }

            btnLogin.Enabled = false;
            btnLogin.Text = "Entrando...";
            lblError.Visible = false;

            try
            {
                var request = new LoginRequest(txtLogin.Text, txtSenha.Text, 1);
                System.Diagnostics.Debug.WriteLine($"🔍 LoginForm - Fazendo login com: Login={txtLogin.Text}, TenantId=1");
                var response = await _authService.LoginAsync(request);
                
                System.Diagnostics.Debug.WriteLine($"🔍 LoginForm - Response recebido: {response != null}");
                if (response != null)
                {
                    System.Diagnostics.Debug.WriteLine($"🔍 LoginForm - Token: {(!string.IsNullOrEmpty(response.Token) ? "OK" : "NULL")}, UsuarioId={response.UsuarioId}, TenantId={response.TenantId}");
                }

                if (response == null)
                {
                    ShowError("Login ou senha inválidos.");
                    return;
                }

                // Save session
                var session = new SessionManager.SessionData
                {
                    Token = response.Token,
                    RefreshToken = response.RefreshToken,
                    UsuarioId = response.UsuarioId,
                    TenantId = response.TenantId,
                    NomeCompleto = response.NomeCompleto,
                    TipoUsuarioId = response.TipoUsuarioId
                };
                
                System.Diagnostics.Debug.WriteLine($"🔍 LoginForm - Tentando salvar sessão...");
                try
                {
                    SessionManager.SaveSession(session);
                    System.Diagnostics.Debug.WriteLine($"✅ LoginForm - Sessão salva com sucesso");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ LoginForm - Erro ao salvar sessão: {ex.Message}");
                    ShowError($"Erro ao salvar sessão: {ex.Message}");
                    return;
                }

                // Aguardar um pouco para garantir que o arquivo foi escrito
                await Task.Delay(100);

                // Verificar se a sessão foi salva corretamente
                var savedSession = SessionManager.LoadSession();
                if (savedSession == null || string.IsNullOrEmpty(savedSession.Token))
                {
                    System.Diagnostics.Debug.WriteLine($"❌ LoginForm - Sessão não foi carregada corretamente após salvar");
                    System.Diagnostics.Debug.WriteLine($"❌ LoginForm - savedSession é NULL: {savedSession == null}");
                    if (savedSession != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ LoginForm - Token é NULL ou vazio: {string.IsNullOrEmpty(savedSession.Token)}");
                    }
                    ShowError("Erro ao salvar sessão. Tente novamente.");
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine($"✅ LoginForm - Sessão verificada: Token={(!string.IsNullOrEmpty(savedSession.Token) ? "OK" : "NULL")}, TenantId={savedSession.TenantId}, UsuarioId={savedSession.UsuarioId}");

                // Open Dashboard - criar nova instância do ApiClient para o Dashboard
                var dashboardApiClient = new ApiClientService();
                dashboardApiClient.SetAuth(response.Token, response.TenantId, response.UsuarioId);
                
                try
                {
                    System.Diagnostics.Debug.WriteLine($"🔍 LoginForm - Criando DashboardForm...");
                    var dashboard = new DashboardForm(dashboardApiClient);
                    System.Diagnostics.Debug.WriteLine($"✅ LoginForm - DashboardForm criado com sucesso");
                    
                    // Esconder este form
                    this.Hide();
                    System.Diagnostics.Debug.WriteLine($"✅ LoginForm - Form escondido");
                    
                    // Mostrar Dashboard
                    dashboard.Show();
                    System.Diagnostics.Debug.WriteLine($"✅ LoginForm - Dashboard mostrado");
                    
                    // Quando o Dashboard fechar, decidir o que fazer baseado no motivo
                    dashboard.FormClosed += (s, e) =>
                    {
                        var isNavigating = NavigationGuard.IsNavigating;
                        var reason = NavigationGuard.CurrentReason;
                        if (isNavigating)
                        {
                            NavigationGuard.Reset();

                            if (reason == NavigationReason.Logout && !this.IsDisposed)
                            {
                                ResetLoginState();
                                this.Show();
                                this.Activate();
                            }

                            System.Diagnostics.Debug.WriteLine($"🔍 LoginForm - Dashboard fechado durante navegação ({reason}). Mantendo LoginForm oculto? {!this.Visible}");
                            return;
                        }

                        System.Diagnostics.Debug.WriteLine("🔍 LoginForm - Dashboard fechado pelo usuário. Encerrando aplicação.");
                        if (!this.IsDisposed)
                        {
                            this.Close();
                        }
                    };
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Erro ao criar Dashboard: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"❌ StackTrace: {ex.StackTrace}");
                    ShowError($"Erro ao abrir dashboard: {ex.Message}");
                    this.Show();
                }
            }
            catch (Exception ex)
            {
                ShowError($"Erro ao fazer login: {ex.Message}");
            }
            finally
            {
                btnLogin.Enabled = true;
                btnLogin.Text = "Login";
            }
        }

        private void ShowError(string message)
        {
            lblError.Text = message;
            lblError.Visible = true;
        }

        private void ResetLoginState()
        {
            txtSenha.Text = string.Empty;
            txtLogin.Text = string.Empty;
            lblError.Visible = false;
            btnLogin.Enabled = true;
            btnLogin.Text = "Login";
        }
    }
}

