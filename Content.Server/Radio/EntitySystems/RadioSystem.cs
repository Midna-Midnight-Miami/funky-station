// SPDX-FileCopyrightText: 2020 Bright0 <55061890+Bright0@users.noreply.github.com>
// SPDX-FileCopyrightText: 2021 Acruid <shatter66@gmail.com>
// SPDX-FileCopyrightText: 2021 Vera Aguilera Puerto <6766154+Zumorica@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Flipp Syder <76629141+vulppine@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Rane <60792108+Elijahrane@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 mirrorcult <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2023 Chief-Engineer <119664036+Chief-Engineer@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Kara <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Slava0135 <40753025+Slava0135@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Visne <39844191+Visne@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 adamsong <adamsong@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 LordCarve <27449516+LordCarve@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 SkaldetSkaeg <impotekh@gmail.com>
// SPDX-FileCopyrightText: 2024 Tadeo <td12233a@gmail.com>
// SPDX-FileCopyrightText: 2024 Tayrtahn <tayrtahn@gmail.com>
// SPDX-FileCopyrightText: 2024 beck-thompson <107373427+beck-thompson@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 slarticodefast <161409025+slarticodefast@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Carrot <carpecarrot@gmail.com>
// SPDX-FileCopyrightText: 2025 Currot <carpecarrot@gmail.com>
// SPDX-FileCopyrightText: 2025 Ecramox <65426878+Ecramox@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 PurpleTranStar <purpletranstars@gmail.com>
// SPDX-FileCopyrightText: 2025 PurpleTranStar <tehevilduckiscoming@gmail.com>
// SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server.Inventory;
using Content.Shared.Inventory;
using Content.Server.Power.Components;
using Content.Server.Radio.Components;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Hands.Components;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Speech;
using Robust.Shared.Containers;
using Content.Shared.Silicons.Laws.Components;
using Robust.Shared.Map;
using System.Linq;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Replays;
using Robust.Shared.Utility;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;

namespace Content.Server.Radio.EntitySystems;

public sealed class RadioSystem : EntitySystem
{
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly IReplayRecordingManager _replay = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ServerInventorySystem _inventorySystem = default!;

    // set used to prevent radio feedback loops.
    private readonly HashSet<string> _messages = new();

    private EntityQuery<TelecomExemptComponent> _exemptQuery;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IntrinsicRadioReceiverComponent, RadioReceiveEvent>(OnIntrinsicReceive);
        SubscribeLocalEvent<IntrinsicRadioTransmitterComponent, EntitySpokeEvent>(OnIntrinsicSpeak);

        _exemptQuery = GetEntityQuery<TelecomExemptComponent>();
    }

    private void OnIntrinsicSpeak(EntityUid uid, IntrinsicRadioTransmitterComponent component, EntitySpokeEvent args)
    {
        if (args.Channel != null && CanTalkChannel(uid, args.Channel.ID))
        {
            SendRadioMessage(uid, args.Message, args.Channel, uid);
            args.Channel = null; // prevent duplicate messages from other listeners.
        }
    }

    private void OnIntrinsicReceive(EntityUid uid, IntrinsicRadioReceiverComponent component, ref RadioReceiveEvent args)
    {
        if (args.Handled)
            return;
        if (TryComp(uid, out ActorComponent? actor))
        {
            _netMan.ServerSendMessage(args.ChatMsg, actor.PlayerSession.Channel);
            args.Handled = true;
        }
    }

    /// <summary>
    /// Send radio message to all active radio listeners
    /// </summary>
    public void SendRadioMessage(EntityUid messageSource, string message, ProtoId<RadioChannelPrototype> channel, EntityUid radioSource, bool escapeMarkup = true)
    {
        SendRadioMessage(messageSource, message, _prototype.Index(channel), radioSource, escapeMarkup: escapeMarkup);
    }

    /// <summary>
    /// Send radio message to all active radio listeners
    /// </summary>
    /// <param name="messageSource">Entity that spoke the message</param>
    /// <param name="radioSource">Entity that picked up the message and will send it, e.g. headset</param>
    public void SendRadioMessage(EntityUid messageSource, string message, RadioChannelPrototype channel, EntityUid radioSource, bool escapeMarkup = true)
    {
        // Midnight - Create a unique message identifier that includes both content and source
        var messageId = $"{message}_{messageSource}_{radioSource}";
        
        // TODO if radios ever garble / modify messages, feedback-prevention needs to be handled better than this.
        if (!_messages.Add(messageId))
            return;

        // Midnight - Play hidden radio sound locally around the sender if applicable with reduced range
        if (TryComp<HiddenRadioComponent>(radioSource, out var hiddenRadio) && 
            IsSoleProvider(messageSource, channel.ID, radioSource))
        {
            _audio.PlayPvs(hiddenRadio.Sound, messageSource, audioParams: AudioParams.Default.WithMaxDistance(7.5f).WithRolloffFactor(2f));
        }

        var evt = new TransformSpeakerNameEvent(messageSource, MetaData(messageSource).EntityName);
        RaiseLocalEvent(messageSource, evt);

        var name = evt.VoiceName;
        name = FormattedMessage.EscapeText(name);

        SpeechVerbPrototype speech;
        if (evt.SpeechVerb != null && _prototype.TryIndex(evt.SpeechVerb, out var evntProto))
            speech = evntProto;
        else
            speech = _chat.GetSpeechVerb(messageSource, message);

        var content = escapeMarkup
            ? FormattedMessage.EscapeText(message)
            : message;

        var wrappedMessage = Loc.GetString(speech.Bold ? "chat-radio-message-wrap-bold" : "chat-radio-message-wrap",
            ("color", channel.Color),
            ("fontType", speech.FontId),
            ("fontSize", speech.FontSize),
            ("verb", Loc.GetString(_random.Pick(speech.SpeechVerbStrings))),
            ("channel", $"\\[{channel.LocalizedName}\\]"),
            ("name", name),
            ("message", content));

        // most radios are relayed to chat, so lets parse the chat message beforehand
        var chat = new ChatMessage(
            ChatChannel.Radio,
            message,
            wrappedMessage,
            NetEntity.Invalid,
            null);
        var chatMsg = new MsgChatMessage { Message = chat };
        var ev = new RadioReceiveEvent(message, messageSource, channel, radioSource, chatMsg);

        var sendAttemptEv = new RadioSendAttemptEvent(channel, radioSource);
        RaiseLocalEvent(ref sendAttemptEv);
        RaiseLocalEvent(radioSource, ref sendAttemptEv);
        var canSend = !sendAttemptEv.Cancelled;

        var sourceMapId = Transform(radioSource).MapID;
        var hasActiveServer = HasActiveServer(sourceMapId, channel.ID);
        var sourceServerExempt = _exemptQuery.HasComp(radioSource);

        // Midnight - Track which entities have already received this message to prevent duplicates
        // Group by the parent entity (the wearer) for headsets, or the entity itself for other radios
        var processedEntities = new HashSet<EntityUid>();

        var radioQuery = EntityQueryEnumerator<ActiveRadioComponent, TransformComponent>();
        while (canSend && radioQuery.MoveNext(out var receiver, out var radio, out var transform))
        {
            if (!radio.ReceiveAllChannels)
            {
                if (!CanListenChannel(receiver, channel.ID) || (TryComp<IntercomComponent>(receiver, out var intercom) &&
                                                             !intercom.SupportedChannels.Contains(channel.ID)))
                    continue;
            }

            if (!channel.LongRange && transform.MapID != sourceMapId && !radio.GlobalReceive)
                continue;

            // don't need telecom server for long range channels or handheld radios and intercoms
            var needServer = !channel.LongRange && !sourceServerExempt;
            if (needServer && !hasActiveServer)
                continue;

            // check if message can be sent to specific receiver
            var attemptEv = new RadioReceiveAttemptEvent(channel, radioSource, receiver);
            RaiseLocalEvent(ref attemptEv);
            RaiseLocalEvent(receiver, ref attemptEv);
            if (attemptEv.Cancelled)
                continue;

            // Imp original - edited to correct behavior for IPCs and Silicons
            if (channel.IntercomOnly && !(HasComp<IntercomComponent>(radioSource) || HasComp<IntercomOnlyBypassComponent>(radioSource)))
                continue;

            // send the message
            RaiseLocalEvent(receiver, ref ev);
        }

        if (name != Name(messageSource))
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Radio message from {ToPrettyString(messageSource):user} as {name} on {channel.LocalizedName}: {message}");
        else
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Radio message from {ToPrettyString(messageSource):user} on {channel.LocalizedName}: {message}");

        _replay.RecordServerMessage(chat);
        _messages.Remove(messageId);
    }

    /// <inheritdoc cref="TelecomServerComponent"/>
    private bool HasActiveServer(MapId mapId, string channelId)
    {
        var servers = EntityQuery<TelecomServerComponent, EncryptionKeyHolderComponent, ApcPowerReceiverComponent, TransformComponent>();
        foreach (var (_, keys, power, transform) in servers)
        {
            if (transform.MapID == mapId &&
                power.Powered &&
                keys.Channels.Contains(channelId))
            {
                return true;
            }
        }
        return false;
    }
    
    // Midnight - MI13 Radio Jacket
    private bool IsSoleProvider(EntityUid user, string channelId, EntityUid radioSource)
    {
        // Get all headsets and radios the user has that can provide this channel
        var providers = GetAllRadioProviders(user, channelId).ToList();

        // Return true if:
        // 1. There's exactly one provider
        // 2. That provider is our hidden radio source
        return providers.Count == 1 && providers[0] == radioSource;
    }

    private IEnumerable<EntityUid> GetAllRadioProviders(EntityUid user, string channelId)
    {
        // Check equipment slots for headsets and active radios
        if (TryComp<InventoryComponent>(user, out var inventory))
        {
            foreach (var slot in inventory.Slots)
            {
                if (_inventorySystem.TryGetSlotEntity(user, slot.Name, out var item))
                {
                    // Check if it's a headset with the required slot and channel
                    if (TryComp<HeadsetComponent>(item, out var headset) && 
                        TryComp<EncryptionKeyHolderComponent>(item, out var headsetKeys) &&
                        headset.Enabled && headset.IsEquipped &&
                        headsetKeys.Channels.Contains(channelId))
                    {
                        yield return item.Value;
                    }
                    // Also check for active radios (handheld, etc.)
                    else if (TryComp<ActiveRadioComponent>(item, out var active) &&
                        TryComp<EncryptionKeyHolderComponent>(item, out var encryption) &&
                        encryption.Channels.Contains(channelId))
                    {
                        yield return item.Value;
                    }
                }
            }
        }

        // Check hands for handheld radios
        if (TryComp<HandsComponent>(user, out var hands))
        {
            foreach (var hand in hands.Hands.Values)
            {
                if (hand.HeldEntity != null)
                {
                    var item = hand.HeldEntity.Value;
                    if (TryComp<ActiveRadioComponent>(item, out var active) &&
                        TryComp<EncryptionKeyHolderComponent>(item, out var encryption) &&
                        encryption.Channels.Contains(channelId))
                    {
                        yield return item;
                    }
                }
            }
        }

        // Check the user themselves (for implanted radios)
        if (TryComp<ActiveRadioComponent>(user, out var userActive) &&
            TryComp<EncryptionKeyHolderComponent>(user, out var userEncryption) &&
            userEncryption.Channels.Contains(channelId))
        {
            yield return user;
        }
    }


    private bool CanTalkChannel(EntityUid uid, string channelId){
        if (TryComp<IntrinsicRadioTransmitterComponent>(uid, out var intrinsicTransmitter) && (intrinsicTransmitter.IntrinsicChannels.Contains(channelId) || intrinsicTransmitter.Channels.Contains(channelId)))
            return true;

        return false;
    }

    private bool CanListenChannel(EntityUid uid, string channelId){

        if (TryComp<ActiveRadioComponent>(uid, out var activeRadio) && (activeRadio.IntrinsicChannels.Contains(channelId) || activeRadio.Channels.Contains(channelId)))
            return true;

        return false;
    }
}
