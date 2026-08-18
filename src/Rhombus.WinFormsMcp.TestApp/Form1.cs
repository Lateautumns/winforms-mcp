namespace Rhombus.WinFormsMcp.TestApp;

public partial class Form1 : Form {
    private readonly BindingSource bindingSource = new();

    public Form1() {
        InitializeComponent();
        bindingSource.DataSource = new BindingModel();
        textBox.DataBindings.Add(
            nameof(TextBox.Text),
            bindingSource,
            nameof(BindingModel.DeviceName),
            formattingEnabled: true,
            updateMode: DataSourceUpdateMode.OnPropertyChanged);
    }

    private sealed class BindingModel {
        public string DeviceName { get; set; } = "Bound device";
    }
}
