using System;

namespace Nekomata.Core.Exceptions;

public class GameExtractionRequiredException : Exception
{
    public string RequiredPath { get; }
    
    public GameExtractionRequiredException(string message, string requiredPath) : base(message)
    {
        RequiredPath = requiredPath;
    }
}
