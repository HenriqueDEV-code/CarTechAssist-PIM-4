using Ganss.Xss;

namespace CarTechAssist.Application.Services
{
    /// <summary>
    /// Serviço de sanitização de entrada para prevenir XSS (Cross-Site Scripting)
    /// </summary>
    public class InputSanitizer
    {
        private readonly HtmlSanitizer _sanitizer;

        public InputSanitizer()
        {
            _sanitizer = new HtmlSanitizer();
            
            // Configurar apenas tags seguras permitidas
            _sanitizer.AllowedTags.Clear();
            _sanitizer.AllowedTags.Add("p");
            _sanitizer.AllowedTags.Add("br");
            _sanitizer.AllowedTags.Add("strong");
            _sanitizer.AllowedTags.Add("em");
            _sanitizer.AllowedTags.Add("u");
            _sanitizer.AllowedTags.Add("ul");
            _sanitizer.AllowedTags.Add("ol");
            _sanitizer.AllowedTags.Add("li");
            _sanitizer.AllowedTags.Add("h1");
            _sanitizer.AllowedTags.Add("h2");
            _sanitizer.AllowedTags.Add("h3");
            _sanitizer.AllowedTags.Add("h4");
            _sanitizer.AllowedTags.Add("h5");
            _sanitizer.AllowedTags.Add("h6");
            
            // Permitir alguns atributos seguros
            _sanitizer.AllowedAttributes.Clear();
            _sanitizer.AllowedAttributes.Add("class");
            
            // Permitir esquemas seguros
            _sanitizer.AllowedSchemes.Clear();
            _sanitizer.AllowedSchemes.Add("http");
            _sanitizer.AllowedSchemes.Add("https");
        }

        /// <summary>
        /// Sanitiza uma string de entrada removendo conteúdo potencialmente perigoso
        /// </summary>
        public string Sanitize(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return input ?? string.Empty;
            
            return _sanitizer.Sanitize(input);
        }

        /// <summary>
        /// Sanitiza uma string mas mantém quebras de linha
        /// </summary>
        public string SanitizePreservingLineBreaks(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return input ?? string.Empty;
            
            // Primeiro sanitiza
            var sanitized = _sanitizer.Sanitize(input);
            
            // Converte quebras de linha em <br/>
            return sanitized.Replace("\r\n", "<br/>")
                           .Replace("\n", "<br/>")
                           .Replace("\r", "<br/>");
        }
    }
}

