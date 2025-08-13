namespace Doxie;

public partial class AddExtensionWindow : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string? _description;
    private string? _inclusion;
    private bool _isRegex;
    private bool _isExclusion;

    public AddExtensionWindow()
    {
        InitializeComponent();
        DataContext = this;
        UpdateControls();
    }

    public string? Inclusion
    {
        get => _inclusion;
        set
        {
            if (_inclusion == value)
                return;

            _inclusion = value;
            OnPropertyChanged();
            UpdateControls();
        }
    }

    public bool IsRegex
    {
        get => _isRegex;
        set
        {
            if (_isRegex == value)
                return;

            _isRegex = value;
            OnPropertyChanged();
            UpdateControls();
        }
    }

    public bool IsExclusion
    {
        get => _isExclusion;
        set
        {
            if (_isExclusion == value)
                return;

            _isExclusion = value;
            OnPropertyChanged();
            UpdateControls();
        }
    }

    public string? Description
    {
        get => _description;
        set
        {
            if (_description == value)
                return;

            _description = value;
            OnPropertyChanged();
            UpdateControls();
        }
    }

    public InclusionDefinition? InclusionDefinition
    {
        get
        {
            var options = InclusionDefinitionOptions.None;
            if (IsRegex)
            {
                options |= InclusionDefinitionOptions.ForceRegex;
            }

            if (IsExclusion)
            {
                options |= InclusionDefinitionOptions.ForceExclusion;
            }
            return InclusionDefinition.Parse(Inclusion, options);
        }
    }

    private void UpdateControls()
    {
        var inclusion = InclusionDefinition;
        ok.IsEnabled = inclusion != null;
        Description = inclusion?.Description ?? string.Empty;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
        base.OnKeyDown(e);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
