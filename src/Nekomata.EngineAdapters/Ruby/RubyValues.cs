using System;
using System.Collections.Generic;
using System.Text;

namespace Nekomata.EngineAdapters.Ruby;

public abstract class RubyValue 
{
}

public class RubyNil : RubyValue { }
public class RubyTrue : RubyValue { }
public class RubyFalse : RubyValue { }

public class RubyFixnum : RubyValue 
{ 
    public int Value { get; set; } 
    public RubyFixnum(int v) { Value = v; }
    public override string ToString() => Value.ToString();
}

public class RubyFloat : RubyValue
{
    public double Value { get; set; }
    public RubyFloat(double v) { Value = v; }
}

public class RubySymbol : RubyValue 
{ 
    public string Name { get; set; } 
    public RubySymbol(string n) { Name = n; }
    
    public override bool Equals(object? obj) => obj is RubySymbol s && s.Name == Name;
    public override int GetHashCode() => Name.GetHashCode();
    public override string ToString() => $":{Name}";
}

public class RubyString : RubyValue 
{ 
    public byte[] Bytes { get; set; }
    public string Decode() => Encoding.UTF8.GetString(Bytes);
    
    public RubyString(byte[] bytes) { Bytes = bytes; }
    public RubyString(string str) { Bytes = Encoding.UTF8.GetBytes(str); }
    public override string ToString() => $"\"{Decode()}\"";
}

public class RubyArray : RubyValue 
{ 
    public List<RubyValue> Elements { get; set; } = new();
}

public class RubyHash : RubyValue 
{ 
    // Use List of Pairs because keys can be any object and Dictionary requires implementation of Equals/HashCode
    public List<KeyValuePair<RubyValue, RubyValue>> Pairs { get; set; } = new();
}

public class RubyObject : RubyValue 
{ 
    public RubySymbol ClassName { get; set; }
    public Dictionary<RubySymbol, RubyValue> Variables { get; set; } = new();
    
    public RubyObject(RubySymbol className) { ClassName = className; }

    public RubyValue? GetVar(string name)
    {
        foreach (var kvp in Variables)
        {
            if (kvp.Key.Name == name) return kvp.Value;
        }
        return null;
    }
}

public class RubyStruct : RubyValue
{
    public RubySymbol ClassName { get; set; }
    public Dictionary<RubySymbol, RubyValue> Members { get; set; } = new();
    public RubyStruct(RubySymbol className) { ClassName = className; }
}

public class RubyUserDef : RubyValue
{
    public RubySymbol ClassName { get; set; }
    public byte[] Data { get; set; }
    public RubyUserDef(RubySymbol className, byte[] data) { ClassName = className; Data = data; }
}

// Represents an object that has instance variables attached (Type 'I')
public class RubyIVar : RubyValue
{
    public RubyValue Value { get; set; }
    public Dictionary<RubySymbol, RubyValue> Variables { get; set; } = new();
    public RubyIVar(RubyValue value) { Value = value; }
}

// Placeholder for Object Link ('@')
public class RubyLink : RubyValue
{
    public int Index { get; set; }
    public RubyLink(int idx) { Index = idx; }
}

// Placeholder for Symbol Link (';')
public class RubySymlink : RubyValue
{
    public int Index { get; set; }
    public RubySymlink(int idx) { Index = idx; }
}
