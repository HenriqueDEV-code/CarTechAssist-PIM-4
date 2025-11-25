namespace CarTechAssist.Desktop.WinForms.Helpers
{
    public enum NavigationReason
    {
        None,
        SwitchForm,
        Logout
    }

    /// <summary>
    /// Utilitário simples para sinalizar quando estamos trocando de tela
    /// para evitar que o fechamento de um form principal encerre toda a aplicação.
    /// </summary>
    public static class NavigationGuard
    {
        private static readonly object _sync = new();
        private static NavigationReason _currentReason = NavigationReason.None;

        public static void Begin(NavigationReason reason)
        {
            lock (_sync)
            {
                _currentReason = reason;
            }
        }

        public static NavigationReason CurrentReason
        {
            get
            {
                lock (_sync)
                {
                    return _currentReason;
                }
            }
        }

        public static bool IsNavigating => CurrentReason != NavigationReason.None;

        public static void Reset()
        {
            lock (_sync)
            {
                _currentReason = NavigationReason.None;
            }
        }
    }
}

