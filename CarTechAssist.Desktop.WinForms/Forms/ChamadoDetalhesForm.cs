using CarTechAssist.Contracts.Tickets;
using CarTechAssist.Contracts.Enums;
using CarTechAssist.Desktop.WinForms.Helpers;
using CarTechAssist.Desktop.WinForms.Services;

namespace CarTechAssist.Desktop.WinForms.Forms
{
    public partial class ChamadoDetalhesForm : Form
    {
        private readonly ApiClientService _apiClient;
        private readonly ChamadosService _chamadosService;
        private readonly long _chamadoId;
        private Label lblTitulo = null!;
        private Label lblNumero = null!;
        private Label lblDataCriacao = null!;
        private Label lblSolicitante = null!;
        private Label lblStatus = null!;
        private Label lblPrioridade = null!;
        private Label lblDescricao = null!;
        private ComboBox cmbStatus = null!;
        private TextBox txtMensagem = null!;
        private Button btnEnviar = null!;
        private RichTextBox rtbMensagens = null!;
        private Button btnVoltar = null!;
        private Label lblMensagemBloqueada = null!;
        private Panel panelInfo = null!;
        private Panel panelChat = null!;
        private byte _tipoUsuarioId;
        private byte _statusAtual;

        // Detalhes de Chamado Forms
        public ChamadoDetalhesForm(ApiClientService apiClient, long chamadoId)
        {
            _apiClient = apiClient;
            var session = SessionManager.LoadSession();
            if (session != null && !string.IsNullOrEmpty(session.Token))
            {
                _apiClient.SetAuth(session.Token, session.TenantId, session.UsuarioId);
                _tipoUsuarioId = session.TipoUsuarioId;
            }
            _chamadosService = new ChamadosService(_apiClient);
            _chamadoId = chamadoId;
            InitializeComponent();
            _ = LoadChamado();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "CarTechAssist - Detalhes do Chamado";
            this.Size = new Size(1400, 900);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(26, 28, 36);
            this.MinimumSize = new Size(1200, 800);
            this.WindowState = FormWindowState.Maximized;

            // Header Panel
            var panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                BackColor = Color.FromArgb(40, 44, 52),
                Padding = new Padding(40, 20, 40, 20)
            };
            this.Controls.Add(panelHeader);

            lblTitulo = new Label
            {
                Text = "📋 Detalhes do Chamado",
                Font = new Font("Segoe UI", 28, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(40, 20)
            };
            panelHeader.Controls.Add(lblTitulo);

            // Subtitle será atualizado quando carregar o chamado

            btnVoltar = new Button
            {
                Text = "← Voltar",
                Location = new Point(1200, 25),
                Size = new Size(120, 40),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11),
                Cursor = Cursors.Hand
            };
            btnVoltar.FlatAppearance.BorderSize = 0;
            // ALTERAÇÃO: Ao clicar, volta para o DashboardForm mantendo o app aberto
            btnVoltar.Click += BtnVoltar_Click_Handler;
            panelHeader.Controls.Add(btnVoltar);

            // Content Panel
            var panelContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(26, 28, 36),
                Padding = new Padding(40),
                AutoScroll = true
            };
            this.Controls.Add(panelContent);

            // Painel de Informações
            panelInfo = new Panel
            {
                Location = new Point(40, 20),
                Size = new Size(1300, 220),
                BackColor = Color.FromArgb(40, 44, 52),
                BorderStyle = BorderStyle.None,
                Padding = new Padding(25, 30, 25, 25)
            };
            panelContent.Controls.Add(panelInfo);

            var lblInfoTitle = new Label
            {
                Text = "ℹ️ Informações do Chamado",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(25, 30)
            };
            panelInfo.Controls.Add(lblInfoTitle);

            int infoY = 75;
            int infoX1 = 25;
            int infoX2 = 650;

            lblNumero = new Label
            {
                Text = "Número: -",
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(infoX1, infoY)
            };
            panelInfo.Controls.Add(lblNumero);

            lblDataCriacao = new Label
            {
                Text = "Data de Criação: -",
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(infoX2, infoY)
            };
            panelInfo.Controls.Add(lblDataCriacao);
            infoY += 35;

            var lblTituloLabel = new Label
            {
                Text = "Título:",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(150, 150, 150),
                AutoSize = true,
                Location = new Point(infoX1, infoY)
            };
            panelInfo.Controls.Add(lblTituloLabel);

            lblDescricao = new Label
            {
                Text = "-",
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(500, 50),
                Location = new Point(infoX1 + 80, infoY)
            };
            panelInfo.Controls.Add(lblDescricao);

            lblSolicitante = new Label
            {
                Text = "Usuário que abriu o chamado: -",
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(infoX2, infoY)
            };
            panelInfo.Controls.Add(lblSolicitante);
            infoY += 35;

            var lblStatusLabel = new Label
            {
                Text = "Status:",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(150, 150, 150),
                AutoSize = true,
                Location = new Point(infoX1, infoY)
            };
            panelInfo.Controls.Add(lblStatusLabel);

            // Status (ComboBox para Agente/Admin, Label para Cliente)
            if (_tipoUsuarioId == 2 || _tipoUsuarioId == 3)
            {
                cmbStatus = new ComboBox
                {
                    Location = new Point(infoX1 + 80, infoY - 5),
                    Size = new Size(200, 30),
                    Font = new Font("Segoe UI", 10),
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                cmbStatus.Items.AddRange(new[] { "Aberto", "Em Andamento", "Pendente",  "Resolvido", "Fechado", "Cancelado" });
                cmbStatus.SelectedIndexChanged += CmbStatus_SelectedIndexChanged;
                panelInfo.Controls.Add(cmbStatus);
            }
            else
            {
                lblStatus = new Label
                {
                    Text = "Carregando...",
                    Font = new Font("Segoe UI", 11),
                    ForeColor = Color.FromArgb(76, 175, 80),
                    AutoSize = true,
                    Location = new Point(infoX1 + 80, infoY)
                };
                panelInfo.Controls.Add(lblStatus);
            }

            var lblPrioridadeLabel = new Label
            {
                Text = "Prioridade:",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(150, 150, 150),
                AutoSize = true,
                Location = new Point(infoX2, infoY)
            };
            panelInfo.Controls.Add(lblPrioridadeLabel);

            lblPrioridade = new Label
            {
                Text = "-",
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(infoX2 + 100, infoY)
            };
            panelInfo.Controls.Add(lblPrioridade);

            // Painel de Chat
            int chatY = 240;
            panelChat = new Panel
            {
                Location = new Point(40, chatY),
                Size = new Size(1300, 500),
                BackColor = Color.FromArgb(40, 44, 52),
                BorderStyle = BorderStyle.None,
                Padding = new Padding(25)
            };
            panelContent.Controls.Add(panelChat);

            var lblChatTitle = new Label
            {
                Text = "💬 Mensagens / Chat",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(25, 25)
            };
            panelChat.Controls.Add(lblChatTitle);

            rtbMensagens = new RichTextBox
            {
                Location = new Point(25, 70),
                Size = new Size(1250, 350),
                BackColor = Color.FromArgb(26, 28, 36),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                Font = new Font("Segoe UI", 10),
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            panelChat.Controls.Add(rtbMensagens);

            // Input de mensagem
            txtMensagem = new TextBox
            {
                Location = new Point(25, 430),
                Size = new Size(1050, 40),
                BackColor = Color.FromArgb(26, 28, 36),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11),
                PlaceholderText = "Digite sua mensagem..."
            };
            txtMensagem.SetPlaceholder("Digite sua mensagem...");
            txtMensagem.KeyDown += TxtMensagem_KeyDown;
            panelChat.Controls.Add(txtMensagem);

            btnEnviar = new Button
            {
                Text = "📤 Enviar",
                Location = new Point(1085, 430),
                Size = new Size(190, 40),
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnEnviar.FlatAppearance.BorderSize = 0;
            btnEnviar.Click += BtnEnviar_Click;
            panelChat.Controls.Add(btnEnviar);

            lblMensagemBloqueada = new Label
            {
                Text = "⚠️ Este chamado está finalizado. Por favor, crie um novo chamado para continuar o atendimento.",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(76, 175, 80),
                AutoSize = false,
                Size = new Size(1250, 40),
                Location = new Point(25, 480),
                Visible = false
            };
            panelChat.Controls.Add(lblMensagemBloqueada);

            this.ResumeLayout(false);
        }

        // Novo método handler para o botão "Voltar"
        private void BtnVoltar_Click_Handler(object? sender, EventArgs e)
        {
            // Abre o DashboardForm e fecha este form.
            var dashboardForm = new DashboardForm(_apiClient);
            dashboardForm.Show();
            this.Close();
        }

        private void TxtMensagem_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                BtnEnviar_Click(sender, e);
            }
        }

        private async void CmbStatus_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (cmbStatus.SelectedIndex >= 0)
            {
                try
                {
                    var novoStatusId = (byte)(cmbStatus.SelectedIndex + 1);
                    var request = new AlterarStatusRequest(novoStatusId);
                    await _chamadosService.AlterarStatusAsync(_chamadoId, request);
                    _statusAtual = novoStatusId;
                    AtualizarEstadoMensagem();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao alterar status: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async Task LoadChamado()
        {
            try
            {
                var chamado = await _chamadosService.ObterAsync(_chamadoId);
                if (chamado != null)
                {
                    if (InvokeRequired)
                    {
                        Invoke(() => UpdateChamadoInfo(chamado));
                    }
                    else
                    {
                        UpdateChamadoInfo(chamado);
                    }

                    // Buscar nome do solicitante separadamente
                    string solicitanteNome = "Não informado";
                    try
                    {
                        var usuariosService = new UsuariosService(_apiClient);
                        var solicitante = await usuariosService.ObterAsync(chamado.SolicitanteUsuarioId);
                        solicitanteNome = solicitante?.NomeCompleto ?? "Não informado";
                    }
                    catch { }

                    if (InvokeRequired)
                    {
                        Invoke(() =>
                        {
                            lblSolicitante.Text = $"Usuário que abriu o chamado: {solicitanteNome}";
                            lblPrioridade.Text = chamado.PrioridadeNome ?? "-";
                            lblDescricao.Text = chamado.Descricao ?? "-";
                            
                            _statusAtual = chamado.StatusId;
                            if (cmbStatus != null)
                            {
                                cmbStatus.SelectedIndex = Math.Max(0, Math.Min(chamado.StatusId - 1, cmbStatus.Items.Count - 1));
                            }
                            else if (lblStatus != null)
                            {
                                lblStatus.Text = GetStatusNome(chamado.StatusId);
                            }

                            AtualizarEstadoMensagem();
                        });
                    }
                    else
                    {
                        lblSolicitante.Text = $"Usuário que abriu o chamado: {solicitanteNome}";
                        lblPrioridade.Text = chamado.PrioridadeNome ?? "-";
                        lblDescricao.Text = chamado.Descricao ?? "-";
                        
                        _statusAtual = chamado.StatusId;
                        if (cmbStatus != null)
                        {
                            cmbStatus.SelectedIndex = Math.Max(0, Math.Min(chamado.StatusId - 1, cmbStatus.Items.Count - 1));
                        }
                        else if (lblStatus != null)
                        {
                            lblStatus.Text = GetStatusNome(chamado.StatusId);
                        }

                        AtualizarEstadoMensagem();
                    }

                    await LoadInteracoes();
                }
                else
                {
                    MessageBox.Show("Chamado não encontrado.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar chamado: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateChamadoInfo(ChamadoDetailDto chamado)
        {
            lblTitulo.Text = $"📋 Detalhes do Chamado - {chamado.Numero}";
            lblNumero.Text = $"Número: {chamado.Numero}";
            lblDataCriacao.Text = $"Data de Criação: {chamado.DataCriacao:dd/MM/yyyy HH:mm}";
        }

        private async Task LoadInteracoes()
        {
            try
            {
                var interacoes = await _chamadosService.ListarInteracoesAsync(_chamadoId);
                rtbMensagens.Clear();
                if (interacoes != null && interacoes.Any())
                {
                    foreach (var interacao in interacoes.OrderBy(i => i.DataCriacao))
                    {
                        AppendMessage(interacao);
                    }
                    rtbMensagens.SelectionStart = rtbMensagens.TextLength;
                    rtbMensagens.ScrollToCaret();
                }
                else
                {
                    rtbMensagens.Text = "💬 Nenhuma mensagem ainda. Inicie a conversa!";
                    rtbMensagens.ForeColor = Color.FromArgb(150, 150, 150);
                    rtbMensagens.SelectionAlignment = HorizontalAlignment.Center;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar interações: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AppendMessage(InteracaoDto interacao)
        {
            rtbMensagens.SelectionStart = rtbMensagens.TextLength;
            rtbMensagens.SelectionLength = 0;

            // Nome do autor
            var autorNome = interacao.AutorNome ?? "Usuário";
            if (interacao.IA_Gerada)
            {
                autorNome += " [IA]";
            }

            rtbMensagens.SelectionColor = interacao.IA_Gerada ? Color.FromArgb(76, 175, 80) : Color.FromArgb(33, 150, 243);
            rtbMensagens.AppendText($"[{interacao.DataCriacao:HH:mm}] {autorNome}:\n");
            
            rtbMensagens.SelectionColor = Color.White;
            rtbMensagens.AppendText($"{interacao.Mensagem}\n\n");
        }

        private string GetStatusNome(byte statusId)
        {
            return statusId switch
            {
                1 => "Aberto",
                2 => "Em Andamento",
                3 => "Pendente",
                4 => "Resolvido",
                5 => "Fechado",
                6 => "Cancelado",
                _ => "Desconhecido"
            };
        }

        private void AtualizarEstadoMensagem()
        {
            var statusBloqueados = new[] { (byte)4, (byte)5, (byte)6 }; // Resolvido, Fechado, Cancelado
            var deveBloquear = _tipoUsuarioId == 1 && statusBloqueados.Contains(_statusAtual);

            if (deveBloquear)
            {
                txtMensagem.Enabled = false;
                txtMensagem.BackColor = Color.FromArgb(50, 50, 50);
                btnEnviar.Enabled = false;
                btnEnviar.BackColor = Color.FromArgb(100, 100, 100);
                lblMensagemBloqueada.Visible = true;
            }
            else
            {
                txtMensagem.Enabled = true;
                txtMensagem.BackColor = Color.FromArgb(26, 28, 36);
                btnEnviar.Enabled = true;
                btnEnviar.BackColor = Color.FromArgb(76, 175, 80);
                lblMensagemBloqueada.Visible = false;
            }
        }

        private async void BtnEnviar_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtMensagem.Text))
                return;

            btnEnviar.Enabled = false;
            txtMensagem.Enabled = false;

            try
            {
                var request = new AdicionarInteracaoRequest(txtMensagem.Text.Trim());
                var interacao = await _chamadosService.AdicionarInteracaoAsync(_chamadoId, request);

                if (interacao != null)
                {
                    txtMensagem.Clear();
                    await LoadInteracoes();

                    // Se for cliente, processar com IA
                    if (_tipoUsuarioId == 1)
                    {
                        try
                        {
                            await _chamadosService.ProcessarMensagemComIAAsync(_chamadoId, request.Mensagem);
                            await LoadInteracoes(); // Recarregar para ver resposta da IA
                        }
                        catch { } // Ignorar erros da IA
                    }
                }
                else
                {
                    MessageBox.Show("Erro ao enviar mensagem.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao enviar mensagem: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnEnviar.Enabled = true;
                txtMensagem.Enabled = true;
                txtMensagem.Focus();
            }
        }
    }
}
