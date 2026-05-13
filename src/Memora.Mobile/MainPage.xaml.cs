using System.Collections.ObjectModel;
using System.Text;
using Memora.Mobile.Models;
using Memora.Mobile.Services;

namespace Memora.Mobile;

public partial class MainPage : ContentPage
{
    private readonly MobilePacketStore _store;
    private readonly ObservableCollection<SavedPacketViewModel> _savedPackets = new();

    private string _packetId;
    private DateTimeOffset _createdAtUtc;
    private bool _bodyTouched;
    private bool _suppressEvents;

    public MainPage()
    {
        InitializeComponent();

        _store = new MobilePacketStore();
        _packetId = Guid.NewGuid().ToString();
        _createdAtUtc = DateTimeOffset.UtcNow;
        _bodyTouched = false;

        InitializeIntents();
        InitializeProposedTypes();

        SavedList.ItemsSource = _savedPackets;

        MetaPacketId.Text = _packetId;
        MetaCreatedAt.Text = _createdAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");

        IntentPicker.SelectedIndex = 0;
        ApplyTemplateForCurrentIntent();
        Render();
        RefreshSavedList();
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

    private MobilePacketDraft BuildDraft()
    {
        var isProposalKind = MobileCaptureIntentCatalog.IsProposalKind(CurrentIntent);
        return new MobilePacketDraft(
            CurrentIntent,
            TitleEntry.Text ?? string.Empty,
            BodyEditor.Text ?? string.Empty,
            TagsEntry.Text ?? string.Empty,
            TargetHintEntry.Text ?? string.Empty,
            DeviceLabelEntry.Text ?? string.Empty,
            isProposalKind && ProposedTypePicker.SelectedIndex > 0
                ? MobileProposedArtifactTypes.All[ProposedTypePicker.SelectedIndex - 1]
                : string.Empty);
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

        var draft = BuildDraft();
        var composition = MobilePacketComposer.Compose(_packetId, _createdAtUtc, draft);
        PreviewEditor.Text = composition.Markdown;
        ErrorsLabel.Text = composition.Errors.Count == 0
            ? string.Empty
            : string.Join("; ", composition.Errors);
        ErrorsLabel.IsVisible = composition.Errors.Count > 0;
    }

    private void RefreshSavedList()
    {
        _savedPackets.Clear();
        foreach (var packet in _store.LoadAll())
        {
            _savedPackets.Add(SavedPacketViewModel.From(packet));
        }

        SavedEmptyLabel.IsVisible = _savedPackets.Count == 0;
    }

    private async void OnCopyClicked(object? sender, EventArgs e)
    {
        var draft = BuildDraft();
        var composition = MobilePacketComposer.Compose(_packetId, _createdAtUtc, draft);
        if (composition.Errors.Count > 0)
        {
            await ShowMessage("Cannot copy", composition.Errors[0]);
            return;
        }

        try
        {
            await Clipboard.Default.SetTextAsync(composition.Markdown);
            await ShowMessage("Copied", "Packet copied to clipboard.");
        }
        catch (Exception ex)
        {
            await ShowMessage("Copy failed", ex.Message);
        }
    }

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        var draft = BuildDraft();
        var composition = MobilePacketComposer.Compose(_packetId, _createdAtUtc, draft);
        if (composition.Errors.Count > 0)
        {
            await ShowMessage("Cannot share", composition.Errors[0]);
            return;
        }

        var fileName = MobilePacketComposer.GenerateFileName(_packetId, _createdAtUtc, draft.Intent, draft.Title, draft.Body);
        var tempPath = Path.Combine(FileSystem.CacheDirectory, fileName);
        try
        {
            await File.WriteAllTextAsync(tempPath, composition.Markdown, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Share Memora packet",
                File = new ShareFile(tempPath, "text/markdown")
            });
        }
        catch (Exception ex)
        {
            await ShowMessage("Share failed", ex.Message);
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var draft = BuildDraft();
        var composition = MobilePacketComposer.Compose(_packetId, _createdAtUtc, draft);
        if (composition.Errors.Count > 0)
        {
            await ShowMessage("Cannot save", composition.Errors[0]);
            return;
        }

        var saved = new SavedMobilePacket(
            _packetId,
            _createdAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            draft.Intent,
            draft.Title ?? string.Empty,
            draft.Body ?? string.Empty,
            draft.TagsCsv ?? string.Empty,
            draft.TargetProjectHint ?? string.Empty,
            draft.DeviceLabel ?? string.Empty,
            draft.ProposedArtifactType ?? string.Empty,
            MobilePacketStore.NowIsoUtc());

        try
        {
            _store.Save(saved);
            RefreshSavedList();
            await ShowMessage("Saved", "Packet saved on this device.");
        }
        catch (Exception ex)
        {
            await ShowMessage("Save failed", ex.Message);
        }
    }

    private async void OnNewClicked(object? sender, EventArgs e)
    {
        var proceed = await DisplayAlertAsync(
            "Start a new packet?",
            "The current form will be cleared. Unsaved changes are lost.",
            "Start new",
            "Cancel");
        if (!proceed)
        {
            return;
        }

        _packetId = Guid.NewGuid().ToString();
        _createdAtUtc = DateTimeOffset.UtcNow;
        _bodyTouched = false;

        _suppressEvents = true;
        try
        {
            TitleEntry.Text = string.Empty;
            TagsEntry.Text = string.Empty;
            TargetHintEntry.Text = string.Empty;
            DeviceLabelEntry.Text = string.Empty;
            ProposedTypePicker.SelectedIndex = 0;
            IntentPicker.SelectedIndex = 0;
        }
        finally
        {
            _suppressEvents = false;
        }

        MetaPacketId.Text = _packetId;
        MetaCreatedAt.Text = _createdAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");
        ApplyTemplateForCurrentIntent();
        Render();
    }

    private void OnOpenSavedClicked(object? sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not string packetId)
        {
            return;
        }

        var saved = _store.LoadAll().FirstOrDefault(p => p.PacketId == packetId);
        if (saved is null)
        {
            return;
        }

        _packetId = saved.PacketId;
        if (DateTimeOffset.TryParse(saved.CreatedAt, out var parsed))
        {
            _createdAtUtc = parsed;
        }

        _suppressEvents = true;
        try
        {
            var intentIndex = MobileCaptureIntentCatalog.All
                .Select((info, idx) => (info, idx))
                .FirstOrDefault(t => t.info.Intent == saved.Intent);
            IntentPicker.SelectedIndex = intentIndex.info is null ? 0 : intentIndex.idx;

            TitleEntry.Text = saved.Title;
            BodyEditor.Text = saved.Body;
            _bodyTouched = !string.IsNullOrWhiteSpace(saved.Body);
            TagsEntry.Text = saved.TagsCsv;
            TargetHintEntry.Text = saved.TargetProjectHint;
            DeviceLabelEntry.Text = saved.DeviceLabel;

            if (!string.IsNullOrEmpty(saved.ProposedArtifactType))
            {
                var typeIndex = MobileProposedArtifactTypes.All.ToList().IndexOf(saved.ProposedArtifactType);
                ProposedTypePicker.SelectedIndex = typeIndex >= 0 ? typeIndex + 1 : 0;
            }
            else
            {
                ProposedTypePicker.SelectedIndex = 0;
            }
        }
        finally
        {
            _suppressEvents = false;
        }

        MetaPacketId.Text = _packetId;
        MetaCreatedAt.Text = _createdAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");
        Render();
    }

    private async void OnDeleteSavedClicked(object? sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not string packetId)
        {
            return;
        }

        var confirm = await DisplayAlertAsync(
            "Delete saved packet?",
            "This removes the saved copy on this device only.",
            "Delete",
            "Cancel");
        if (!confirm)
        {
            return;
        }

        _store.Delete(packetId);
        RefreshSavedList();
    }

    private Task ShowMessage(string title, string message) =>
        DisplayAlertAsync(title, message, "OK");

    private sealed record SavedPacketViewModel(
        string PacketId,
        string IntentLabel,
        string DisplayTitle,
        string MetaLine)
    {
        public static SavedPacketViewModel From(SavedMobilePacket packet)
        {
            var info = MobileCaptureIntentCatalog.GetByIntent(packet.Intent);
            var title = string.IsNullOrWhiteSpace(packet.Title) ? "(untitled)" : packet.Title.Trim();
            var shortId = packet.PacketId.Length >= 8 ? packet.PacketId[..8] : packet.PacketId;
            var savedAt = string.IsNullOrWhiteSpace(packet.SavedAt) ? packet.CreatedAt : packet.SavedAt;
            return new SavedPacketViewModel(
                packet.PacketId,
                info.DisplayName,
                title,
                $"saved {savedAt} · id {shortId}");
        }
    }
}
