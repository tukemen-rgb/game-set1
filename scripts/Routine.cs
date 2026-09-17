using Godot;
using System;
using System.Collections.Generic;

/// <param name="Pos">Local position in the body's parent coordinate system.</param>
/// <param name="RotY">Arrival orientation in radians, as for Node3D.Rotation.Y.</param>
public sealed record Stop(double FromHour, Vector3 Pos, float RotY);

/// <summary>Direct movement at 1.2 m/s. The caller supplies clear routes in a metre-scale parent.</summary>
public sealed class Routine
{
    private const float WalkSpeed = 1.2f;
    private readonly Node3D _body;
    private readonly Stop[] _stops;
    private int _active = -1;
    public Vector3 Target { get; private set; }

    public Routine(Node3D body, IReadOnlyList<Stop> stops)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(stops);
        if (!GodotObject.IsInstanceValid(body))
            throw new ArgumentException("Body must be a live Node3D.", nameof(body));
        _body = body;
        Target = body.Position;
        _stops = new Stop[stops.Count];
        for (int i = 0; i < stops.Count; i++)
        {
            Stop stop = stops[i];
            if (stop == null || !double.IsFinite(stop.FromHour) || stop.FromHour < 0 || stop.FromHour > 24
                || !float.IsFinite(stop.Pos.X) || !float.IsFinite(stop.Pos.Y) || !float.IsFinite(stop.Pos.Z)
                || !float.IsFinite(stop.RotY))
                throw new ArgumentException($"Invalid stop {i}.", nameof(stops));
            _stops[i] = stop;
        }
        Array.Sort(_stops, (a, b) => a.FromHour.CompareTo(b.FromHour));
        for (int i = 1; i < _stops.Length; i++)
            if (_stops[i - 1].FromHour == _stops[i].FromHour)
                throw new ArgumentException("Stop times must be unique.", nameof(stops));
    }

    public void Update(double hour, double delta)
    {
        if (!double.IsFinite(hour))
            throw new ArgumentOutOfRangeException(nameof(hour));
        if (!double.IsFinite(delta) || delta < 0)
            throw new ArgumentOutOfRangeException(nameof(delta));
        if (!GodotObject.IsInstanceValid(_body))
            return; // Scene changes can free the body before its owner's next update.

        int next = -1;
        for (int i = 0; i < _stops.Length && _stops[i].FromHour <= hour; i++)
            next = i;
        if (next != _active)
        {
            _active = next;
            Target = next < 0 ? _body.Position : _stops[next].Pos;
            if (OS.GetEnvironment("DEBUG_MSG") == "1")
                GD.Print($"[routine] {_body.Name} → {(next < 0 ? "待機" : _stops[next].FromHour.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))}");
        }
        if (_active < 0 || delta == 0)
            return;
        float step = (float)Math.Min(float.MaxValue, delta * WalkSpeed);
        _body.Position = _body.Position.MoveToward(Target, step);
        if (_body.Position == Target)
        {
            Vector3 rotation = _body.Rotation;
            rotation.Y = _stops[_active].RotY;
            _body.Rotation = rotation;
        }
    }
}
