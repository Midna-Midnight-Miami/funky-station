// SPDX-FileCopyrightText: 2023 Slava0135 <40753025+Slava0135@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 SkaldetSkaeg <impotekh@gmail.com>
// SPDX-FileCopyrightText: 2025 Crude Oil <124208219+CroilBird@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Chat;
using Content.Shared.Radio;

namespace Content.Server.Radio;

[ByRefEvent]
public struct RadioReceiveEvent
{
    public readonly string Message;
    public readonly EntityUid MessageSource;
    public readonly RadioChannelPrototype Channel;
    public readonly EntityUid RadioSource;
    public readonly MsgChatMessage ChatMsg;
    public bool Handled;

    public RadioReceiveEvent(string message, EntityUid messageSource, RadioChannelPrototype channel, EntityUid radioSource, MsgChatMessage chatMsg)
    {
        Message = message;
        MessageSource = messageSource;
        Channel = channel;
        RadioSource = radioSource;
        ChatMsg = chatMsg;
        Handled = false;
    }
}

/// <summary>
/// Event raised on the parent entity of a headset radio when a radio message is received
/// </summary>
[ByRefEvent]
public readonly record struct HeadsetRadioReceiveRelayEvent(RadioReceiveEvent RelayedEvent);

/// <summary>
/// Use this event to cancel sending message per receiver
/// </summary>
[ByRefEvent]
public record struct RadioReceiveAttemptEvent(RadioChannelPrototype Channel, EntityUid RadioSource, EntityUid RadioReceiver)
{
    public readonly RadioChannelPrototype Channel = Channel;
    public readonly EntityUid RadioSource = RadioSource;
    public readonly EntityUid RadioReceiver = RadioReceiver;
    public bool Cancelled = false;
}

/// <summary>
/// Use this event to cancel sending message to every receiver
/// </summary>
[ByRefEvent]
public record struct RadioSendAttemptEvent(RadioChannelPrototype Channel, EntityUid RadioSource)
{
    public readonly RadioChannelPrototype Channel = Channel;
    public readonly EntityUid RadioSource = RadioSource;
    public bool Cancelled = false;
}
