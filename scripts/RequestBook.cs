using Godot;
using System;
using System.Collections.Generic;
using DataObject = Godot.Collections.Dictionary;

public enum RequestKind { Deliver, Fix, Find, Listen }

public sealed record RequestDef(int Id, string Client, Vector2 ClientPos, string Title, string Ask,
    RequestKind Kind, Vector2 Target, string TargetLabel, string NeedTool, string Reward, string Done, int Unlocks);

/// <summary>Reads the whole book atomically. Invalid data never exposes a partial quest chain.</summary>
public static class RequestBook
{
    public static IReadOnlyList<RequestDef> Load(string path = "res://data/requests.json")
    {
        if (string.IsNullOrWhiteSpace(path))
            return Failed(path, "empty path");
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
            return Failed(path, $"cannot open file ({FileAccess.GetOpenError()})");
        using var json = new Json();
        if (json.Parse(file.GetAsText()) != Error.Ok)
            return Failed(path, $"line {json.GetErrorLine()}: {json.GetErrorMessage()}");
        if (json.Data.VariantType != Variant.Type.Array)
            return Failed(path, "root must be an array");

        try
        {
            using var rows = json.Data.AsGodotArray();
            var result = new List<RequestDef>(rows.Count);
            var ids = new HashSet<int>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].VariantType != Variant.Type.Dictionary)
                    throw new FormatException($"row {i + 1}: expected an object");
                using var row = rows[i].AsGodotDictionary();
                int id = Integer(row, "Id", i + 1);
                if (id <= 0 || !ids.Add(id))
                    throw new FormatException($"row {i + 1}: Id must be positive and unique");
                string kindName = Text(row, "Kind", i + 1);
                RequestKind kind = kindName switch
                {
                    "Deliver" => RequestKind.Deliver,
                    "Fix" => RequestKind.Fix,
                    "Find" => RequestKind.Find,
                    "Listen" => RequestKind.Listen,
                    _ => throw new FormatException($"row {i + 1}: unknown Kind '{kindName}'")
                };
                int unlocks = row.ContainsKey("Unlocks") ? Integer(row, "Unlocks", i + 1) : 0;
                if (unlocks < 0)
                    throw new FormatException($"row {i + 1}: Unlocks must be nonnegative");
                result.Add(new RequestDef(id, Text(row, "Client", i + 1), Position(row, "ClientPos", i + 1),
                    Text(row, "Title", i + 1), Text(row, "Ask", i + 1), kind,
                    Position(row, "Target", i + 1), Text(row, "TargetLabel", i + 1),
                    row.ContainsKey("NeedTool") ? Text(row, "NeedTool", i + 1) : "",
                    Text(row, "Reward", i + 1), Text(row, "Done", i + 1), unlocks));
            }
            foreach (RequestDef request in result)
                if (request.Unlocks != 0 && !ids.Contains(request.Unlocks))
                    throw new FormatException($"Id {request.Id}: Unlocks refers to missing Id {request.Unlocks}");
            LogCount(result.Count);
            return result.AsReadOnly(); // Keep file order; Id is an identifier, not a list index.
        }
        catch (FormatException error)
        {
            return Failed(path, error.Message);
        }
    }

    private static Variant Required(DataObject row, string key, int index)
    {
        if (!row.TryGetValue(key, out Variant value))
            throw new FormatException($"row {index}: missing {key}");
        return value;
    }

    private static string Text(DataObject row, string key, int index)
    {
        Variant value = Required(row, key, index);
        if (value.VariantType != Variant.Type.String)
            throw new FormatException($"row {index}: {key} must be a string");
        return value.AsString();
    }

    private static double Number(Variant value, string field)
    {
        if (value.VariantType != Variant.Type.Int && value.VariantType != Variant.Type.Float)
            throw new FormatException($"{field} must be a number");
        double number = value.AsDouble();
        if (!double.IsFinite(number))
            throw new FormatException($"{field} must be finite");
        return number;
    }

    private static int Integer(DataObject row, string key, int index)
    {
        double number = Number(Required(row, key, index), $"row {index}: {key}");
        if (number < int.MinValue || number > int.MaxValue || number != Math.Truncate(number))
            throw new FormatException($"row {index}: {key} must be a 32-bit integer");
        return (int)number;
    }

    private static Vector2 Position(DataObject row, string key, int index)
    {
        Variant value = Required(row, key, index);
        if (value.VariantType != Variant.Type.Array)
            throw new FormatException($"row {index}: {key} must be [x, z]");
        using var pair = value.AsGodotArray();
        if (pair.Count != 2)
            throw new FormatException($"row {index}: {key} must contain exactly two numbers");
        double x = Number(pair[0], $"row {index}: {key}[0]");
        double z = Number(pair[1], $"row {index}: {key}[1]");
        if (Math.Abs(x) > float.MaxValue || Math.Abs(z) > float.MaxValue)
            throw new FormatException($"row {index}: {key} is outside Vector2 range");
        return new Vector2((float)x, (float)z);
    }

    private static IReadOnlyList<RequestDef> Failed(string path, string reason)
    {
        GD.PushWarning($"[requests] {path}: {reason}");
        LogCount(0);
        return Array.Empty<RequestDef>();
    }

    private static void LogCount(int count)
    {
        if (OS.GetEnvironment("DEBUG_MSG") == "1")
            GD.Print($"[requests] {count} 件");
    }
}
