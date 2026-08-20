using System;
using System.Windows.Forms;

using Rhombus.WinFormsMcp.RuntimeBridge;

namespace Rhombus.WinFormsMcp.Net472Consumer
{
    /// <summary>
    /// Minimal consumer form shared by the SDK-style and legacy .NET Framework
    /// 4.7.2 consumer projects. The fixed control names are asserted by the
    /// verification script (verify-net472-consumers.ps1), so do not rename them.
    /// </summary>
    public sealed class MainForm : Form
    {
        public MainForm()
        {
            Name = "net472ConsumerForm";
            Text = "Net472 RuntimeBridge Consumer";
            var verifyButton = new Button
            {
                Name = "verifyButton",
                Text = "Verify",
                Location = new System.Drawing.Point(12, 12),
                Size = new System.Drawing.Size(140, 32)
            };
            Controls.Add(verifyButton);
            Shown += OnShown;
            FormClosed += OnFormClosed;
        }

        private void OnShown(object sender, EventArgs e)
        {
            // The handle exists here, so the bridge can bind this form as its
            // UI dispatch target.
            McpRuntimeBridge.StartForControl(this);
        }

        private void OnFormClosed(object sender, FormClosedEventArgs e)
        {
            McpRuntimeBridge.Stop();
        }
    }
}
