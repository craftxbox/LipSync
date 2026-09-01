// .NET 9 – OVR-compatible surface without native bindings.
// Purpose: Provide the public API shape used by apps expecting OVR LipSync.

using System;

namespace OpenLipSync.Inference.OVRCompat;

// ----------------------------
// Shared enums & Frame (OVR-like)
// ----------------------------

[Serializable]
public sealed class Frame
{
	public int frameNumber;
	public int frameDelay;
	public float[] Visemes = new float[VisemeCount];
	public float laughterScore;

	public void CopyInput(Frame input)
	{
		frameNumber = input.frameNumber;
		frameDelay = input.frameDelay;
		input.Visemes.CopyTo(Visemes, 0);
		laughterScore = input.laughterScore;
	}

	public void Reset()
	{
		frameNumber = 0;
		frameDelay = 0;
		Array.Clear(Visemes, 0, Visemes.Length);
		laughterScore = 0f;
	}

	public const int VisemeCount = 15;
}
