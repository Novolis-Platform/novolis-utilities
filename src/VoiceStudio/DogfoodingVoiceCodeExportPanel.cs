using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Audio.Voice.Design;
using Novolis.Avalonia.Voice;
using VoiceStudio.Voice;

namespace VoiceStudio;

/// <summary>GPR + Voice Studio ATC/Bridge code export.</summary>
internal sealed class VoiceStudioVoiceCodeExportPanel : DockPanel, IVoicePresetCodeExport
{
    private readonly ComboBox _template = new();
    private readonly TextBox _code = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        MinHeight = 160,
        FontFamily = "Consolas,Courier New,monospace",
    };
    private VoicePresetDraft? _draft;

    public VoiceStudioVoiceCodeExportPanel()
    {
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            Margin = new Thickness(8, 8, 8, 4),
        };
        header.Children.Add(new TextBlock
        {
            Text = "Generated C#",
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 8, 0, 4),
        });
        foreach (VoicePresetCodeTemplate t in Enum.GetValues<VoicePresetCodeTemplate>())
            _template.Items.Add(t);
        foreach (VoiceStudioVoiceCodeTemplate t in Enum.GetValues<VoiceStudioVoiceCodeTemplate>())
            _template.Items.Add(t);
        _template.SelectedIndex = 0;
        _template.SelectionChanged += (_, _) => RefreshCode();
        Grid.SetColumn(_template, 1);
        header.Children.Add(_template);
        var copy = new Button { Content = "Copy", Margin = new Thickness(8, 0, 0, 0) };
        copy.Click += async (_, _) =>
        {
            if (string.IsNullOrEmpty(_code.Text))
                return;
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is not null)
                await clipboard.SetTextAsync(_code.Text);
        };
        Grid.SetColumn(copy, 2);
        header.Children.Add(copy);

        DockPanel.SetDock(header, Dock.Top);
        Children.Add(header);
        Children.Add(_code);
    }

    Control IVoicePresetCodeExport.View => this;

    public void Bind(VoicePresetDraft? draft)
    {
        _draft = draft;
        RefreshCode();
    }

    private void RefreshCode()
    {
        if (_draft is null || _template.SelectedItem is null)
        {
            _code.Text = string.Empty;
            return;
        }

        var validation = VoicePresetValidation.Validate(_draft);
        if (!validation.IsValid)
        {
            _code.Text = "// Fix validation errors:\n// " + string.Join("\n// ", validation.Errors);
            return;
        }

        try
        {
            _code.Text = _template.SelectedItem switch
            {
                VoicePresetCodeTemplate gpr => VoicePresetCodeEmitter.Emit(_draft, gpr),
                VoiceStudioVoiceCodeTemplate voice => VoiceStudioVoiceCodeEmitter.Emit(_draft, voice),
                _ => string.Empty,
            };
        }
        catch (Exception ex)
        {
            _code.Text = $"// {ex.Message}";
        }
    }
}

