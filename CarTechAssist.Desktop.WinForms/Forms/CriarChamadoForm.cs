using CarTechAssist.Contracts.Tickets;
using CarTechAssist.Desktop.WinForms.Helpers;
using CarTechAssist.Desktop.WinForms.Services;

namespace CarTechAssist.Desktop.WinForms.Forms
{
    public partial class CriarChamadoForm : Form
    {
        private readonly ApiClientService _apiClient;
        private readonly ChamadosService _chamadosService;
        private readonly CategoriasService _categoriasService;
        private readonly UsuariosService _usuariosService;
        
        private TextBox txtTitulo = null!;
        private TextBox txtDescricao = null!;
        private ComboBox cmbCategoria = null!;
        private ComboBox cmbPrioridade = null!;
        private ComboBox cmbCanal = null!;
        private ComboBox cmbSolicitante = null!;
        private ComboBox cmbResponsavel = null!;
        private TextBox txtSLA = null!;
        private Button btnCriar = null!;
        private Button btnCancelar = null!;
        private Label lblErro = null!;
        
        private byte _tipoUsuarioId;
        private int _usuarioId;

        public CriarChamadoForm(ApiClientService apiClient)
        {
            _apiClient = apiClient;
            _chamadosService = new ChamadosService(_apiClient);
            _categoriasService = new CategoriasService(_apiClient);
            _usuariosService = new UsuariosService(_apiClient);
            
            var session = SessionManager.LoadSession();
            if (session != null)
            {
                _tipoUsuarioId = session.TipoUsuarioId;
                _usuarioId = session.UsuarioId;
            }
            
            InitializeComponent();
            LoadData();
        }

        private async void LoadData()
        {
            try
            {
                // Carregar categorias
                var categorias = await _categoriasService.ListarAsync();
                if (categorias != null)
                {
                    cmbCategoria.Items.Clear();
                    cmbCategoria.Items.Add(new { CategoriaId = 0, Nome = "Selecione uma categoria" });
                    foreach (var cat in categorias)
                    {
                        cmbCategoria.Items.Add(cat);
                    }
                    cmbCategoria.DisplayMember = "Nome";
                    cmbCategoria.ValueMember = "CategoriaId";
                    cmbCategoria.SelectedIndex = 0;
                }

                // Se for Técnico ou Admin, carregar usuários
                if (_tipoUsuarioId == 2 || _tipoUsuarioId == 3)
                {
                    var usuarios = await _usuariosService.ListarAsync(ativo: true, page: 1, pageSize: 1000);
                    if (usuarios?.Items != null)
                    {
                        cmbSolicitante.Items.Clear();
                        cmbSolicitante.Items.Add(new { UsuarioId = 0, NomeCompleto = "Selecione o solicitante" });
                        foreach (var user in usuarios.Items)
                        {
                            cmbSolicitante.Items.Add(user);
                        }
                        cmbSolicitante.DisplayMember = "NomeCompleto";
                        cmbSolicitante.ValueMember = "UsuarioId";
                        cmbSolicitante.SelectedIndex = 0;

                        var tecnicos = await _usuariosService.ListarAsync(tipo: 2, ativo: true, page: 1, pageSize: 1000);
                        if (tecnicos?.Items != null)
                        {
                            cmbResponsavel.Items.Clear();
                            cmbResponsavel.Items.Add(new { UsuarioId = 0, NomeCompleto = "Selecione o responsável (opcional)" });
                            foreach (var tec in tecnicos.Items)
                            {
                                cmbResponsavel.Items.Add(tec);
                            }
                            cmbResponsavel.DisplayMember = "NomeCompleto";
                            cmbResponsavel.ValueMember = "UsuarioId";
                            cmbResponsavel.SelectedIndex = 0;
                        }
                    }
                }
                else
                {
                    // Cliente não precisa ver campos de solicitante/responsável
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar dados: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "Novo Chamado";
            this.Size = new Size(800, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(26, 26, 26);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var panelContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(26, 26, 26),
                Padding = new Padding(30),
                AutoScroll = true
            };
            this.Controls.Add(panelContent);

            int y = 20;

            // Título
            var lblTitle = new Label
            {
                Text = "Novo Chamado",
                Font = new Font("Segoe UI", 24, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(30, y)
            };
            panelContent.Controls.Add(lblTitle);
            y += 50;

            // Erro
            lblErro = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(220, 53, 69),
                AutoSize = true,
                Location = new Point(30, y),
                Visible = false
            };
            panelContent.Controls.Add(lblErro);
            y += 30;

            // Título do chamado
            var lblTitulo = new Label
            {
                Text = "Título *",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(30, y)
            };
            panelContent.Controls.Add(lblTitulo);
            y += 25;

            txtTitulo = new TextBox
            {
                Location = new Point(30, y),
                Size = new Size(700, 30),
                Font = new Font("Segoe UI", 10),
                MaxLength = 200
            };
            panelContent.Controls.Add(txtTitulo);
            y += 45;

            // Descrição
            var lblDescricao = new Label
            {
                Text = "Descrição *",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(30, y)
            };
            panelContent.Controls.Add(lblDescricao);
            y += 25;

            txtDescricao = new TextBox
            {
                Location = new Point(30, y),
                Size = new Size(700, 120),
                Font = new Font("Segoe UI", 10),
                Multiline = true,
                MaxLength = 10000,
                ScrollBars = ScrollBars.Vertical
            };
            panelContent.Controls.Add(txtDescricao);
            y += 135;

            // Categoria e Prioridade
            var lblCategoria = new Label
            {
                Text = "Categoria *",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(30, y)
            };
            panelContent.Controls.Add(lblCategoria);

            cmbCategoria = new ComboBox
            {
                Location = new Point(30, y + 25),
                Size = new Size(340, 30),
                Font = new Font("Segoe UI", 10),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            panelContent.Controls.Add(cmbCategoria);

            var lblPrioridade = new Label
            {
                Text = "Prioridade *",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(390, y)
            };
            panelContent.Controls.Add(lblPrioridade);

            cmbPrioridade = new ComboBox
            {
                Location = new Point(390, y + 25),
                Size = new Size(340, 30),
                Font = new Font("Segoe UI", 10),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbPrioridade.Items.AddRange(new[] { "Baixa", "Média", "Alta", "Urgente" });
            cmbPrioridade.SelectedIndex = 1; // Média
            cmbPrioridade.SelectedIndexChanged += CmbPrioridade_SelectedIndexChanged;
            panelContent.Controls.Add(cmbPrioridade);
            y += 70;

            // Canal e SLA
            var lblCanal = new Label
            {
                Text = "Canal *",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(30, y)
            };
            panelContent.Controls.Add(lblCanal);

            cmbCanal = new ComboBox
            {
                Location = new Point(30, y + 25),
                Size = new Size(340, 30),
                Font = new Font("Segoe UI", 10),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbCanal.Items.AddRange(new[] { "Web", "Desktop", "Mobile", "Chatbot" });
            cmbCanal.SelectedIndex = 1; // Desktop
            panelContent.Controls.Add(cmbCanal);

            var lblSLA = new Label
            {
                Text = "SLA Estimado",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(390, y)
            };
            panelContent.Controls.Add(lblSLA);

            txtSLA = new TextBox
            {
                Location = new Point(390, y + 25),
                Size = new Size(340, 30),
                Font = new Font("Segoe UI", 10),
                ReadOnly = true,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White
            };
            panelContent.Controls.Add(txtSLA);
            y += 70;

            // Solicitante e Responsável (apenas para Técnico/Admin)
            if (_tipoUsuarioId == 2 || _tipoUsuarioId == 3)
            {
                var lblSolicitante = new Label
                {
                    Text = "Solicitante *",
                    Font = new Font("Segoe UI", 10),
                    ForeColor = Color.White,
                    AutoSize = true,
                    Location = new Point(30, y)
                };
                panelContent.Controls.Add(lblSolicitante);

                cmbSolicitante = new ComboBox
                {
                    Location = new Point(30, y + 25),
                    Size = new Size(340, 30),
                    Font = new Font("Segoe UI", 10),
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                panelContent.Controls.Add(cmbSolicitante);

                var lblResponsavel = new Label
                {
                    Text = "Responsável",
                    Font = new Font("Segoe UI", 10),
                    ForeColor = Color.White,
                    AutoSize = true,
                    Location = new Point(390, y)
                };
                panelContent.Controls.Add(lblResponsavel);

                cmbResponsavel = new ComboBox
                {
                    Location = new Point(390, y + 25),
                    Size = new Size(340, 30),
                    Font = new Font("Segoe UI", 10),
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                panelContent.Controls.Add(cmbResponsavel);
                y += 70;
            }

            // Botões
            btnCancelar = new Button
            {
                Text = "Cancelar",
                Location = new Point(500, y),
                Size = new Size(110, 40),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnCancelar.FlatAppearance.BorderSize = 0;
            btnCancelar.Click += (s, e) => this.Close();
            panelContent.Controls.Add(btnCancelar);

            btnCriar = new Button
            {
                Text = "Criar Chamado",
                Location = new Point(620, y),
                Size = new Size(110, 40),
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnCriar.FlatAppearance.BorderSize = 0;
            btnCriar.Click += BtnCriar_Click;
            panelContent.Controls.Add(btnCriar);

            // Calcular SLA inicial
            CalcularSLA();

            this.ResumeLayout(false);
        }

        private void CmbPrioridade_SelectedIndexChanged(object? sender, EventArgs e)
        {
            CalcularSLA();
        }

        private void CalcularSLA()
        {
            int horas = cmbPrioridade.SelectedIndex switch
            {
                0 => 72, // Baixa
                1 => 48, // Média
                2 => 24, // Alta
                3 => 4,  // Urgente
                _ => 48
            };

            var dataEstimada = DateTime.UtcNow.AddHours(horas);
            txtSLA.Text = dataEstimada.ToString("dd/MM/yyyy HH:mm");
        }

        private async void BtnCriar_Click(object? sender, EventArgs e)
        {
            // Validações
            if (string.IsNullOrWhiteSpace(txtTitulo.Text))
            {
                MostrarErro("Título é obrigatório.");
                return;
            }

            if (string.IsNullOrWhiteSpace(txtDescricao.Text))
            {
                MostrarErro("Descrição é obrigatória.");
                return;
            }

            if (cmbCategoria.SelectedIndex <= 0)
            {
                MostrarErro("Categoria é obrigatória. Selecione uma categoria.");
                return;
            }

            int solicitanteId = _usuarioId;
            if (_tipoUsuarioId == 2 || _tipoUsuarioId == 3)
            {
                if (cmbSolicitante.SelectedIndex <= 0)
                {
                    MostrarErro("Solicitante é obrigatório.");
                    return;
                }
                var solicitante = cmbSolicitante.SelectedItem;
                var prop = solicitante?.GetType().GetProperty("UsuarioId");
                if (prop != null)
                {
                    var value = prop.GetValue(solicitante);
                    if (value is int id)
                    {
                        solicitanteId = id;
                    }
                    else
                    {
                        solicitanteId = _usuarioId;
                    }
                }
            }

            try
            {
                btnCriar.Enabled = false;
                var categoria = cmbCategoria.SelectedItem;
                var categoriaIdProp = categoria?.GetType().GetProperty("CategoriaId");
                int categoriaId = 0;
                if (categoriaIdProp != null)
                {
                    var value = categoriaIdProp.GetValue(categoria);
                    if (value is int id)
                    {
                        categoriaId = id;
                    }
                }

                int? responsavelId = null;
                if ((_tipoUsuarioId == 2 || _tipoUsuarioId == 3) && cmbResponsavel != null && cmbResponsavel.SelectedIndex > 0)
                {
                    var responsavel = cmbResponsavel.SelectedItem;
                    var responsavelIdProp = responsavel?.GetType().GetProperty("UsuarioId");
                    if (responsavelIdProp != null)
                    {
                        var value = responsavelIdProp.GetValue(responsavel);
                        if (value is int id)
                        {
                            responsavelId = id;
                        }
                    }
                }

                var prioridadeId = (byte)(cmbPrioridade.SelectedIndex + 1);
                var canalId = (byte)(cmbCanal.SelectedIndex + 1);
                var slaEstimado = DateTime.UtcNow.AddHours(prioridadeId switch
                {
                    1 => 72,
                    2 => 48,
                    3 => 24,
                    4 => 4,
                    _ => 48
                });

                var request = new CriarChamadoRequest(
                    Titulo: txtTitulo.Text.Trim(),
                    Descricao: txtDescricao.Text.Trim(),
                    CategoriaId: categoriaId,
                    PrioridadeId: prioridadeId,
                    CanalId: canalId,
                    SolicitanteUsuarioId: solicitanteId,
                    ResponsavelUsuarioId: responsavelId,
                    SLA_EstimadoFim: slaEstimado
                );

                var resultado = await _chamadosService.CriarAsync(request);
                if (resultado != null)
                {
                    MessageBox.Show("Chamado criado com sucesso!", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    MostrarErro("Erro ao criar chamado. Tente novamente.");
                }
            }
            catch (Exception ex)
            {
                MostrarErro($"Erro ao criar chamado: {ex.Message}");
            }
            finally
            {
                btnCriar.Enabled = true;
            }
        }

        private void MostrarErro(string mensagem)
        {
            lblErro.Text = mensagem;
            lblErro.Visible = true;
        }
    }
}

