using Godot;
using System;
using System.Collections.Generic;
using DayClock = GameSet1.Time.DayClock;

/// <summary>Independent module acceptance checks; does not load or modify either game scene.</summary>
public partial class BenriyaModules : SceneTree
{
    private int _checks;
    private readonly string _fixture = $"user://benriya-module-{Guid.NewGuid():N}.json";

    public override void _Initialize()
    {
        try
        {
            CheckRequests();
            CheckClock();
            CheckRoutine();
            GD.Print($"[benriya-modules] ok ({_checks} assertions)");
            Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError($"[benriya-modules] FAIL: {error}");
            Quit(1);
        }
        finally
        {
            if (FileAccess.FileExists(_fixture))
                DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(_fixture));
        }
    }

    private void Check(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private void Rejects(Action action, string message)
    {
        try { action(); }
        catch (ArgumentException) { Check(true, message); return; }
        Check(false, message);
    }

    private IReadOnlyList<RequestDef> Fixture(string text)
    {
        using (var file = FileAccess.Open(_fixture, FileAccess.ModeFlags.Write))
        {
            if (file == null) throw new InvalidOperationException("Cannot write test fixture");
            file.StoreString(text);
        }
        return RequestBook.Load(_fixture);
    }

    private void CheckRequests()
    {
        var book = RequestBook.Load();
        Check(book.Count == 5, "five requests");
        Check(book[0].Client == "管理人" && book[4].Reward == "鍵束", "Japanese text");
        Check(book[0].Ask.Contains('\n') && !book[0].Ask.Contains("\\n"), "decoded newline");
        Check(book[1].ClientPos == new Vector2(-18.6f, -1.1f), "fractional x/z position");
        Check(book[3].Kind == RequestKind.Find && book[3].Unlocks == 0, "photo request");
        Check(book[0].Unlocks == 2 && book[1].Unlocks == 3 && book[2].Unlocks == 5, "unlock chain");
        Check(((IList<RequestDef>)book).IsReadOnly, "read-only book");
        string source = FileAccess.GetFileAsString("res://data/requests.json");
        using var json = new Json();
        Check(json.Parse(source) == Error.Ok, "source JSON");
        using var rows = json.Data.AsGodotArray();

        using (var optional = rows.Duplicate(true))
        {
            using var first = optional[0].AsGodotDictionary();
            first.Remove("NeedTool"); first.Remove("Unlocks");
            var result = Fixture(Json.Stringify(optional));
            Check(result.Count == 5 && result[0].NeedTool == "" && result[0].Unlocks == 0, "optional defaults");
        }
        Check(Fixture("[]").Count == 0, "empty array");
        Check(Fixture("[{broken").Count == 0, "malformed JSON");
        Check(Fixture("{}").Count == 0, "wrong root");
        Check(Fixture("[false]").Count == 0, "wrong row");
        Check(RequestBook.Load(_fixture + ".missing").Count == 0, "missing file");
        Check(RequestBook.Load("").Count == 0, "empty path");

        void Bad(string key, Variant value, string name)
        {
            using var changed = rows.Duplicate(true);
            using var last = changed[4].AsGodotDictionary();
            last[key] = value;
            Check(Fixture(Json.Stringify(changed)).Count == 0, name + ": no partial result");
        }
        Bad("Id", 1, "duplicate ID");
        Bad("Id", 1.5, "fractional ID");
        Bad("Kind", "Teleport", "unknown kind");
        Bad("Kind", "0", "numeric kind string");
        Bad("ClientPos", "36,8.5", "wrong vector type");
        using var shortPair = new Godot.Collections.Array { 36.0 };
        Bad("Target", shortPair, "short vector");
        using var badPair = new Godot.Collections.Array { "36", 8.5 };
        Bad("Target", badPair, "string coordinate");
        using var hugePair = new Godot.Collections.Array { 1e100, 8.5 };
        Bad("Target", hugePair, "coordinate overflow");
        Bad("Unlocks", 999, "dangling unlock");
        Bad("NeedTool", true, "wrong optional type");
        using (var missing = rows.Duplicate(true))
        {
            using var last = missing[4].AsGodotDictionary();
            last.Remove("Title");
            Check(Fixture(Json.Stringify(missing)).Count == 0, "missing required key");
        }
        using (var reversed = rows.Duplicate(true))
        {
            reversed.Reverse();
            var result = Fixture(Json.Stringify(reversed));
            Check(result.Count == 5 && result[0].Id == 5 && result[4].Id == 1, "file order preserved");
        }
        GD.Print("[requests-test] ok");
    }

    private void CheckClock()
    {
        DayClock.SelfTest();
        var clock = new DayClock();
        Check(clock.Day == 1 && clock.Hour == 8 && clock.SecondsPerHour == 20, "clock defaults");
        clock.Advance(120);
        Check(clock.Hour == 14 && clock.Warm == 0, "14:00 warmth start");
        clock.Advance(50);
        Check(Math.Abs(clock.Warm - Math.Pow(0.5, 1.3)) < 1e-6, "16:30 warmth curve");
        clock.Advance(double.MaxValue);
        Check(clock.Day == 1 && clock.Hour == 19, "large delta clamps");
        Check(clock.SunRotationDegrees.DistanceTo(new Vector3(-10, -30, 0)) < 1e-4, "19:00 sun");
        clock.NextDay();
        Check(clock.Day == 2 && clock.Hour == 8, "next day reset");
        clock.SecondsPerHour = 40;
        clock.Advance(40);
        Check(clock.Hour == 9, "changed clock speed");
        Rejects(() => clock.SecondsPerHour = 0, "zero speed");
        Rejects(() => clock.SecondsPerHour = double.NaN, "NaN speed");
        Rejects(() => clock.Advance(-1), "negative time");
        Rejects(() => clock.Advance(double.PositiveInfinity), "infinite time");
        var fine = new DayClock(); var coarse = new DayClock();
        for (int i = 0; i < 600; i++) fine.Advance(1.0 / 60.0);
        coarse.Advance(10);
        Check(Math.Abs(fine.Hour - coarse.Hour) < 1e-10, "clock frame independence");
    }

    private void CheckRoutine()
    {
        var body = new Node3D { Name = "ModuleTestResident", Position = new Vector3(0, 2, 0) };
        Root.AddChild(body);
        var stops = new[] { new Stop(17, new Vector3(0, 2, 4), 1), new Stop(8, new Vector3(6, 2, 0), 0), new Stop(12, new Vector3(6, 2, 4), 2) };
        try
        {
            var routine = new Routine(body, stops);
            routine.Update(7.99, 1);
            Check(body.Position == new Vector3(0, 2, 0), "wait before first stop");
            routine.Update(8, 1);
            Check(routine.Target == new Vector3(6, 2, 0), "8:00 target");
            Check(Math.Abs(body.Position.X - 1.2f) < 1e-6 && body.Position.Y == 2, "1.2 m/s local movement");
            routine.Update(11.999, 0);
            Check(routine.Target == new Vector3(6, 2, 0), "before noon");
            routine.Update(12, 0);
            Check(routine.Target == new Vector3(6, 2, 4) && Math.Abs(body.Position.X - 1.2f) < 1e-6, "noon switches without teleport");
            routine.Update(12, 100);
            Check(body.Position == routine.Target && Math.Abs(body.Rotation.Y - 2) < 1e-6, "arrival without overshoot and radians");
            routine.Update(17, 0);
            Check(routine.Target == new Vector3(0, 2, 4), "17:00 target");
            routine.Update(19, 100);
            Check(body.Position == routine.Target, "evening arrival");
            routine.Update(8, 0);
            Check(routine.Target == new Vector3(6, 2, 0), "time rewind selects morning");
            var empty = new Routine(body, Array.Empty<Stop>());
            Vector3 before = body.Position;
            empty.Update(12, 100);
            Check(body.Position == before, "empty schedule");
            Rejects(() => new Routine(body, new[] { stops[0], stops[0] }), "duplicate stop times");
            Rejects(() => routine.Update(8, -1), "negative movement delta");
            Rejects(() => routine.Update(double.NaN, 1), "NaN hour");
            Rejects(() => new Routine(body, new[] { new Stop(8, new Vector3(float.NaN, 0, 0), 0) }), "NaN target");

            Vector3 Walk(int frames)
            {
                body.Position = Vector3.Zero;
                var r = new Routine(body, new[] { new Stop(8, new Vector3(10, 0, 0), 0) });
                for (int i = 0; i < frames; i++) r.Update(8, 1.0 / frames);
                return body.Position;
            }
            Check(Walk(1).DistanceTo(Walk(60)) < 1e-5, "movement frame independence");
            body.Free();
            routine.Update(12, 1); // Must tolerate a freed scene node.
            Check(true, "freed body");
        }
        finally { if (GodotObject.IsInstanceValid(body)) body.Free(); }
        GD.Print("[routine-test] ok");
    }
}
