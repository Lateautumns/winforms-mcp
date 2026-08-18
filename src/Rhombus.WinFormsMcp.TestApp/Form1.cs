namespace Rhombus.WinFormsMcp.TestApp;

public partial class Form1 : Form {
    private readonly BindingSource _bindingSource = new();

    public Form1() {
        InitializeComponent();
        _bindingSource.DataSource = new BindingModel();
        textBox.DataBindings.Add(
            nameof(TextBox.Text),
            _bindingSource,
            nameof(BindingModel.DeviceName),
            formattingEnabled: true,
            updateMode: DataSourceUpdateMode.OnPropertyChanged);
    }

    private sealed class BindingModel {
        public string DeviceName { get; set; } = "Bound device";
    }
}