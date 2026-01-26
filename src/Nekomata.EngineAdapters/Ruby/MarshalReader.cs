using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Nekomata.EngineAdapters.Ruby;

public class MarshalReader
{
    private BinaryReader _reader;
    private List<RubySymbol> _symbols = new();
    private List<RubyValue> _objects = new();

    public MarshalReader(Stream stream)
    {
        _reader = new BinaryReader(stream);
    }

    public RubyValue Read()
    {
        byte major = _reader.ReadByte();
        byte minor = _reader.ReadByte();
        if (major != 4 || minor != 8) throw new Exception($"Unsupported Marshal version: {major}.{minor}");
        return ReadValue();
    }

    private RubyValue ReadValue()
    {
        byte type = _reader.ReadByte();
        return ReadValueByType((char)type);
    }

    private RubyValue ReadValueByType(char type)
    {
        switch (type)
        {
            case '0': return new RubyNil();
            case 'T': return new RubyTrue();
            case 'F': return new RubyFalse();
            case 'i': return new RubyFixnum(ReadInt());
            case '"': return ReadString();
            case ':': return ReadSymbol();
            case ';': return ReadSymlink();
            case '@': return ReadLink();
            case '[': return ReadArray();
            case '{': return ReadHash();
            case 'o': return ReadObject();
            case 'u': return ReadUserDef();
            case 'I': return ReadIVar();
            case 'f': return ReadFloat();
            // case 'S': return ReadStruct(); // Implement if needed
            default: throw new Exception($"Unknown type '{type}' at position {_reader.BaseStream.Position - 1}");
        }
    }

    private int ReadInt()
    {
        sbyte c = (sbyte)_reader.ReadByte();
        if (c == 0) return 0;
        if (c > 0)
        {
            if (c > 4) return c - 5;
            int len = c;
            int result = 0;
            for (int i = 0; i < len; i++)
            {
                result |= (int)_reader.ReadByte() << (8 * i);
            }
            return result;
        }
        else
        {
            if (c < -4) return c + 5;
            int len = -c;
            int result = -1;
            for (int i = 0; i < len; i++)
            {
                result &= ~(0xFF << (8 * i));
                result |= (int)_reader.ReadByte() << (8 * i);
            }
            return result;
        }
    }

    private RubySymbol ReadSymbol()
    {
        int len = ReadInt();
        byte[] bytes = _reader.ReadBytes(len);
        string name = Encoding.UTF8.GetString(bytes);
        var sym = new RubySymbol(name);
        _symbols.Add(sym);
        return sym;
    }

    private RubySymbol ReadSymlink()
    {
        int idx = ReadInt();
        if (idx < 0 || idx >= _symbols.Count) throw new Exception($"Symlink index out of range: {idx}");
        return _symbols[idx];
    }

    private RubyValue ReadLink()
    {
        int idx = ReadInt();
        // Since we might be in the middle of constructing the object graph, 
        // we can return the object if it exists.
        // NOTE: In C# implementation, if we added the object reference BEFORE reading its content,
        // we should be able to return it here.
        if (idx < 0 || idx >= _objects.Count) throw new Exception($"Link index out of range: {idx}");
        return _objects[idx]; 
        // Note: If we use a wrapper for forward references, we'd return that. 
        // But since Marshal format guarantees definition before reference (except for circular, which relies on object identity),
        // we should be fine if we add to _objects immediately on creation.
    }

    private RubyString ReadString()
    {
        int len = ReadInt();
        byte[] bytes = _reader.ReadBytes(len);
        var str = new RubyString(bytes);
        _objects.Add(str);
        return str;
    }

    private RubyArray ReadArray()
    {
        int len = ReadInt();
        var arr = new RubyArray();
        _objects.Add(arr);
        for (int i = 0; i < len; i++)
        {
            arr.Elements.Add(ReadValue());
        }
        return arr;
    }

    private RubyHash ReadHash()
    {
        int len = ReadInt();
        var hash = new RubyHash();
        _objects.Add(hash);
        for (int i = 0; i < len; i++)
        {
            var key = ReadValue();
            var value = ReadValue();
            hash.Pairs.Add(new KeyValuePair<RubyValue, RubyValue>(key, value));
        }
        return hash;
    }

    private RubyObject ReadObject()
    {
        RubySymbol className = (RubySymbol)ReadValue(); // Symbol follows 'o'
        var obj = new RubyObject(className);
        _objects.Add(obj);
        
        int varCount = ReadInt();
        for (int i = 0; i < varCount; i++)
        {
            var key = (RubySymbol)ReadValue();
            var val = ReadValue();
            obj.Variables[key] = val;
        }
        return obj;
    }

    private RubyUserDef ReadUserDef()
    {
        RubySymbol className = (RubySymbol)ReadValue();
        int len = ReadInt();
        byte[] data = _reader.ReadBytes(len);
        var userDef = new RubyUserDef(className, data);
        _objects.Add(userDef);
        return userDef;
    }

    private RubyValue ReadIVar()
    {
        // 'I' wraps another value
        var val = ReadValue();
        
        // If val is already an object in _objects list, we should modify it directly if possible?
        // Or wrap it in RubyIVar?
        // In Ruby, IVars on Strings/Arrays/etc don't change the object identity.
        // But for our generic model, if we wrap it, we might break Links pointing to the inner object.
        // CRITICAL: If 'val' is in _objects, any existing Link points to 'val'.
        // If we return a new RubyIVar wrapper, future Links might point to this wrapper? No, Links use index.
        // The inner object was added to _objects. 
        // Does 'I' structure itself get added to _objects? NO.
        
        // So we should attach variables to the existing object if possible.
        // But RubyValue is abstract.
        
        var ivarWrapper = new RubyIVar(val);
        
        // Note: We do NOT add ivarWrapper to _objects.
        
        int count = ReadInt();
        for (int i = 0; i < count; i++)
        {
            var k = (RubySymbol)ReadValue();
            var v = ReadValue();
            ivarWrapper.Variables[k] = v;
        }
        
        return ivarWrapper;
    }

    private RubyFloat ReadFloat()
    {
        int len = ReadInt();
        byte[] bytes = _reader.ReadBytes(len);
        string str = Encoding.UTF8.GetString(bytes);
        double val = 0;
        double.TryParse(str, out val);
        var f = new RubyFloat(val);
        _objects.Add(f);
        return f;
    }
}
