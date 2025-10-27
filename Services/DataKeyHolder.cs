using Microsoft.JSInterop;

public static class DataKeyHolder
{
    // Holds a reference to the decrypted AES key (lives in JS; .NET keeps a proxy ref)
    public static IJSObjectReference? Ref { get; set; }
    public static void Clear() => Ref = null;
}
