using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Nekomata.EngineAdapters.Ruby;

public class MarshalWriter
{
    private BinaryWriter _writer;
    private Dictionary<string, int> _symbols = new();
    private Dictionary<RubyValue, int> _objects = new();
    private int _symbolCount = 0;
    private int _objectCount = 0;

    public MarshalWriter(Stream stream)
    {
        _writer = new BinaryWriter(stream);
    }

    public void Write(RubyValue val)
    {
        _writer.Write((byte)4);
        _writer.Write((byte)8);
        WriteValue(val);
    }

    private void WriteValue(RubyValue val)
    {
        if (val is RubyNil) { _writer.Write((byte)'0'); return; }
        if (val is RubyTrue) { _writer.Write((byte)'T'); return; }
        if (val is RubyFalse) { _writer.Write((byte)'F'); return; }
        if (val is RubyFixnum fix) { _writer.Write((byte)'i'); WriteInt(fix.Value); return; }
        
        // Check for Links
        // Note: RubyIVar is not "linked" as itself, but its inner value might be.
        // However, the wrapper itself is transient. 
        // If we have RubyIVar, we write 'I' then recurse. 
        // We DO NOT check link for RubyIVar itself.
        if (val is RubyIVar ivar)
        {
            _writer.Write((byte)'I');
            WriteValue(ivar.Value);
            WriteInt(ivar.Variables.Count);
            foreach (var kvp in ivar.Variables)
            {
                WriteValue(kvp.Key);
                WriteValue(kvp.Value);
            }
            return;
        }

        if (val is RubySymbol sym)
        {
            if (_symbols.TryGetValue(sym.Name, out int idx))
            {
                _writer.Write((byte)';');
                WriteInt(idx);
            }
            else
            {
                _writer.Write((byte)':');
                WriteInt(sym.Name.Length); // Bytes length? Usually ASCII/UTF8 matches
                byte[] b = Encoding.UTF8.GetBytes(sym.Name);
                _writer.Write(b);
                _symbols[sym.Name] = _symbolCount++;
            }
            return;
        }

        // For other objects, check Object Link
        if (_objects.TryGetValue(val, out int objIdx))
        {
            _writer.Write((byte)'@');
            WriteInt(objIdx);
            return;
        }

        // Add to objects (except special ones handled above)
        // Note: We add to _objects map BEFORE writing content for some types?
        // Actually, in Marshal, the object is added when definition starts.
        _objects[val] = _objectCount++;

        if (val is RubyString str)
        {
            _writer.Write((byte)'"');
            WriteInt(str.Bytes.Length);
            _writer.Write(str.Bytes);
        }
        else if (val is RubyArray arr)
        {
            _writer.Write((byte)'[');
            WriteInt(arr.Elements.Count);
            foreach (var e in arr.Elements) WriteValue(e);
        }
        else if (val is RubyHash hash)
        {
            _writer.Write((byte)'{');
            WriteInt(hash.Pairs.Count);
            foreach (var p in hash.Pairs)
            {
                WriteValue(p.Key);
                WriteValue(p.Value);
            }
        }
        else if (val is RubyObject obj)
        {
            _writer.Write((byte)'o');
            WriteValue(obj.ClassName);
            WriteInt(obj.Variables.Count);
            foreach (var kvp in obj.Variables)
            {
                WriteValue(kvp.Key);
                WriteValue(kvp.Value);
            }
        }
        else if (val is RubyUserDef user)
        {
            _writer.Write((byte)'u');
            WriteValue(user.ClassName);
            WriteInt(user.Data.Length);
            _writer.Write(user.Data);
        }
        else if (val is RubyFloat f)
        {
            _writer.Write((byte)'f');
            string s = f.Value.ToString("G"); // Standard format?
            byte[] b = Encoding.UTF8.GetBytes(s);
            WriteInt(b.Length);
            _writer.Write(b);
        }
        else
        {
            throw new Exception($"Unsupported type for writing: {val.GetType().Name}");
        }
    }

    private void WriteInt(int v)
    {
        if (v == 0) { _writer.Write((byte)0); return; }
        if (v > 0 && v < 123) { _writer.Write((byte)(v + 5)); return; }
        if (v < 0 && v > -124) { _writer.Write((byte)(v - 5)); return; }

        byte[] bytes = BitConverter.GetBytes(v);
        int len = 0;
        if (v > 0)
        {
            if (v <= 0xFF) len = 1;
            else if (v <= 0xFFFF) len = 2;
            else if (v <= 0xFFFFFF) len = 3;
            else len = 4;
            _writer.Write((byte)len);
            for (int i = 0; i < len; i++) _writer.Write(bytes[i]);
        }
        else
        {
            if (v >= -0x100) len = 1;
            else if (v >= -0x10000) len = 2;
            else if (v >= -0x1000000) len = 3;
            else len = 4;
            _writer.Write((byte)(-len));
            for (int i = 0; i < len; i++) _writer.Write(bytes[i]);
        }
    }
}
