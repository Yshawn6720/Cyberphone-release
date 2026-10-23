using System.Collections.Concurrent;
using System.Globalization;
using Dalamud.Interface;
using Dalamud.Plugin;
using D3D11 = SharpDX.Direct3D11;

namespace Aetherphone.Core.Video;

internal static class DxHandler
{
	internal static D3D11.Device? Device { get; private set; }

	private static readonly ConcurrentDictionary<string, Action> pendingRenderWork = new();
	private static readonly Lock drainLock = new();

	internal static event Action? OnPresent;

	private static IUiBuilder? pumpBuilder;
	private static long renderKeySequence;

	internal static void Initialise(IDalamudPluginInterface pluginInterface)
	{
		if (pumpBuilder is not null)
		{
			return;
		}

		Device = new D3D11.Device(pluginInterface.UiBuilder.DeviceHandle);
		pumpBuilder = pluginInterface.UiBuilder;
		pumpBuilder.Draw += PumpRenderThread;
	}

	internal static string NewRenderKey(string prefix) =>
		prefix + "-" + Interlocked.Increment(ref renderKeySequence).ToString(CultureInfo.InvariantCulture);

	internal static void RunOnRenderThread(string key, Action work)
	{
		pendingRenderWork[key] = work;
	}

	internal static void CancelRenderThreadWork(string key)
	{
		lock (drainLock)
		{
			pendingRenderWork.TryRemove(key, out _);
		}
	}

	private static void PumpRenderThread()
	{
		DrainRenderWork();
		NotifyPresent();
	}

	private static void DrainRenderWork()
	{
		if (pendingRenderWork.IsEmpty)
		{
			return;
		}

		lock (drainLock)
		{
			foreach (var key in pendingRenderWork.Keys)
			{
				if (pendingRenderWork.TryRemove(key, out var work))
				{
					try
					{
						work();
					}
					catch (Exception e)
					{
						AepLog.Error($"[DxHandler] Render-thread callback '{key}' failed: {e}");
					}
				}
			}
		}
	}

	private static void NotifyPresent()
	{
		try
		{
			OnPresent?.Invoke();
		}
		catch (Exception e)
		{
			AepLog.Error($"[DxHandler] OnPresent subscriber failed: {e}");
		}
	}

	public static void Dispose()
	{
		if (pumpBuilder is not null)
		{
			pumpBuilder.Draw -= PumpRenderThread;
			pumpBuilder = null;
		}

		pendingRenderWork.Clear();
		OnPresent = null;

		Device = null;
	}
}
