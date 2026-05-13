using Memora.Mobile.Models;
using Memora.Mobile.Services;

namespace Memora.Mobile;

public partial class MainPage : ContentPage
{
    private readonly string _packetId;
    private readonly DateTimeOffset _createdAtUtc;
    private bool _bodyTouched;
    private bool _suppressEvents;

    public MainPage()
    {
        InitializeComponent();

        _packetId = Guid.NewGuid().ToString();
        _createdAtUtc = DateTimeOffset.UtcNow;
        _bodyTouched = false;

        InitializeIntents();
        InitializeProposedTypes();

        MetaPacketId.Text = _packetId;
        MetaCreatedAt.Text = _createdAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");

        IntentPicker.SelectedIndex = 0;
        ApplyTemplateForCurrentIntent();
        Render();
    }

    private void InitializeIntents()
    {
        _suppressEvents = true;
        try
        {
            IntentPicker.ItemsSource = MobileCaptureIntentCatalog.All
                .Select(info => info.DisplayName)
                .ToList();
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    private void InitializeProposedTypes()
    {
        _suppressEvents = true;
        try
        {
            var items = new List<string> { "(unspecified)" };
            items.AddRange(MobileProposedArtifactTypes.All);
            ProposedTypePicker.ItemsSource = items;
            ProposedTypePicker.SelectedIndex = 0;
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    private MobileCaptureIntent CurrentIntent =>
        MobileCaptureIntentCatalog.All[Math.Max(IntentPicker.SelectedIndex, 0)].Intent;

    private void OnIntentChanged(object? sender, EventArgs e)
    {
        if (_suppressEvents) return;
        ApplyTemplateForCurrentIntent();
        Render();
    }

    private void OnFormChanged(object? sender, EventArgs e)
    {
        if (_suppressEvents) return;
        Render();
    }

    private void OnBodyChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressEvents) return;
        _bodyTouched = !string.IsNullOrWhiteSpace(BodyEditor.Text);
        Render();
    }

    private void ApplyTemplateForCurrentIntent()
    {
        var info = MobileCaptureIntentCatalog.GetByIntent(CurrentIntent);
        if (!_bodyTouched || string.IsNullOrWhiteSpace(BodyEditor.Text))
        {
            _suppressEvents = true;
            try
            {
                BodyEditor.Text = info.BodyTemplate;
                _bodyTouched = false;
            }
            finally
            {
                _suppressEvents = false;
            }
        }
    }

    private void Render()
    {
        var info = MobileCaptureIntentCatalog.GetByIntent(CurrentIntent);
        IntentHintLabel.Text = info.IntentHint;
        MetaLifecycleTarget.Text = info.LifecycleTargetSchemaValue;

        var isProposalKind = MobileCaptureIntentCatalog.IsProposalKind(CurrentIntent);
        ProposedTypeContainer.IsVisible = isProposalKind;
        if (!isProposalKind && ProposedTypePicker.SelectedIndex > 0)
        {
            _suppressEvents = true;
            try
            {
                ProposedTypePicker.SelectedIndex = 0;
            }
            finally
            {
                _suppressEvents = false;
            }
        }

        var draft = new MobilePacketDraft(
            CurrentIntent,
            TitleEntry.Text ?? string.Empty,
            BodyEditor.Text ?? string.Empty,
            TagsEntry.Text ?? string.Empty,
            TargetHintEntry.Text ?? string.Empty,
            DeviceLabelEntry.Text ?? string.Empty,
            isProposalKind && ProposedTypePicker.SelectedIndex > 0
                ? MobileProposedArtifactTypes.All[ProposedTypePicker.SelectedIndex - 1]
                : string.Empty);

        var composition = MobilePacketComposer.Compose(_packetId, _createdAtUtc, draft);
        PreviewEditor.Text = composition.Markdown;
        ErrorsLabel.Text = composition.Errors.Count == 0
            ? string.Empty
            : string.Join("; ", composition.Errors);
        ErrorsLabel.IsVisible = composition.Errors.Count > 0;
    }
}
