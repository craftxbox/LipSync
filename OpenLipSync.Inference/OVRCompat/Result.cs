namespace OpenLipSync.Inference.OVRCompat;

public enum Result
{
    Success = 0,
    Unknown = -2200,
    CannotCreateContext = -2201,
    InvalidParam = -2202,
    BadSampleRate = -2203,
    MissingDLL = -2204,
    BadVersion = -2205,
    UndefinedFunction = -2206
}