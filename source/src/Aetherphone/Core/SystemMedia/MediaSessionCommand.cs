namespace Aetherphone.Core.SystemMedia;

internal enum MediaSessionCommandKind : byte
{
    Play,
    Pause,
    TogglePlayPause,
    Next,
    Previous,
    Seek,
    SetShuffle,
    SetRepeat,
    SetVolume,
}

internal readonly record struct MediaSessionCommand(MediaSessionCommandKind Kind, long Argument);
