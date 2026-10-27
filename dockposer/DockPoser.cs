// DockPoser.cs -- the Dock Poser window (2026-10-28, Tefa: "we need Dock poser to be what Grip Zones has for turning the wrists and
// all those settings, while the left hand is actually docked on any of the weapon grip spots, mags and bullets from the pouch
// included ... please make Dock Poser a separate thing"). It follows where the left hand is docked right now (the spot the game
// script writes into zones_state.json: slide / pump / bolt, slide from the left, magazine, rounds from the pouch, grip handle,
// drum, insert point, a hand pair's landing spot) and shows six sliders for THAT gun and spot: move the hand forward / up /
// right, tilt, turn and roll it about the gun's own axes. Every change goes into reframework/data/visceral_re8/dock_poses.json
// at once; the plugin (src/dockpose.cpp) eases the hand there within a second. Not docked: the sliders wait.
// Built from the same files as Grip Zones (build.bat: csc /main:DockPoserProgram), so it shares its look, sliders, tick boxes,
// the controller nudge through Window View, the game folder setting and the gun names.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

static class DockPoserProgram {
    [STAThread]
    static int Main(string[] args) {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new DockPoserUi());
        return 0;
    }
}

class DockPoserUi : Form {
    const int POLL_MS = 250, SAVE_DEBOUNCE_MS = 120, FIND_GAME_EVERY_MS = 5000;
    const double CM_PER_M = 100.0;
    static readonly Color Back = Color.FromArgb(28, 30, 38), Ink = Color.FromArgb(235, 235, 240), Dim = Color.FromArgb(155, 160, 175);
    static readonly Color Good = Color.FromArgb(90, 200, 120), Warn = Color.FromArgb(230, 190, 90), Bad = Color.FromArgb(225, 120, 95);

    readonly Settings settings = new Settings();   // the same game folder as Grip Zones
    readonly ZoneFile file = new ZoneFile();
    readonly Timer poll = new Timer(), saveTimer = new Timer(), nudgeTimer = new Timer();
    readonly Nudge nudge = new Nudge();
    readonly Label status, where, nudgeNote;
    readonly ValueBox wrist;
    DateTime lastFind = DateTime.MinValue;
    int gun, spot;
    string loadedKey = "";
    bool syncing;

    public DockPoserUi() {
        Text = "Dock Poser";
        BackColor = Back; ForeColor = Ink;
        Font = new Font("Segoe UI", 11f);
        ClientSize = new Size(620, 620);
        MinimumSize = new Size(520, 520);
        StartPosition = FormStartPosition.CenterScreen;
        string ico = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "grip_zones.ico");
        try { Icon = new Icon(ico); } catch (Exception) { }
        DoubleBuffered = true;
        Backdrop.Init(this, ico, Back);

        var body = new SkinPanel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        Controls.Add(body);
        wrist = new ValueBox("THE LEFT WRIST, DOCKED", "this gun and this spot only; the hand eases there", new[] {
            new ValueRow("z", "forward / back", "cm", -15, 15, 0.5), new ValueRow("y", "up / down", "cm", -15, 15, 0.5),
            new ValueRow("x", "right / left", "cm", -15, 15, 0.5), new ValueRow("rx", "tilt fingers up / down", "°", -180, 180, 1),
            new ValueRow("ry", "turn left / right", "°", -180, 180, 1), new ValueRow("rz", "roll", "°", -180, 180, 1) },
            () => { if (!syncing) { saveTimer.Stop(); saveTimer.Start(); } }, (t, h) => ZonesUi.Btn(t, h));
        body.Controls.Add(wrist);

        var top = new SkinPanel { Dock = DockStyle.Top, Height = 150, Tint = Color.FromArgb(120, Back) };
        Controls.Add(top);
        nudgeNote = new SkinLabel { Dock = DockStyle.Top, Height = 30, ForeColor = Dim, Padding = new Padding(10, 4, 0, 0) };
        var clear = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, BackColor = Color.Transparent, Padding = new Padding(6, 2, 6, 0) };
        clear.Controls.Add(ZonesUi.Btn("CLEAR ALL TICKS", (s, e) => Ticks.ClearAll()));
        where = new SkinLabel { Dock = DockStyle.Top, Height = 40, Font = new Font("Segoe UI Semibold", 13f), ForeColor = Ink, Padding = new Padding(10, 8, 0, 0) };
        status = new SkinLabel { Dock = DockStyle.Top, Height = 34, ForeColor = Dim, Padding = new Padding(10, 8, 0, 0) };
        top.Controls.Add(nudgeNote);
        top.Controls.Add(clear);
        top.Controls.Add(where);
        top.Controls.Add(status);

        string folder = settings.Get("game", null);
        if (!ZoneFile.LooksLikeVillage(folder)) folder = ZoneFile.FindVillage();
        if (folder != null) { try { file.Use(folder); } catch (Exception) { } }

        poll.Interval = POLL_MS; poll.Tick += (s, e) => Poll(); poll.Start();
        saveTimer.Interval = SAVE_DEBOUNCE_MS; saveTimer.Tick += (s, e) => { saveTimer.Stop(); Save(); };
        nudgeTimer.Interval = 15; nudgeTimer.Tick += (s, e) => { bool started; double step = nudge.Poll(Ticks.Count > 0, out started); if (step != 0) Ticks.StepAll((int)step); };
        nudgeTimer.Start();
        Ticks.Changed += ShowNudge;
        ShowNudge();
        Poll();
    }

    protected override void OnPaintBackground(PaintEventArgs e) { Backdrop.PaintUnder(this, e.Graphics, e.ClipRectangle); }
    protected override CreateParams CreateParams { get { return Backdrop.Composited(base.CreateParams); } }

    void ShowNudge() {
        int n = Ticks.Count;
        nudgeNote.Text = n == 0 ? "tick sliders: X -1, Y -5, A +1, B +5" : !nudge.Linked ? n + " ticked: start Window View, click the left stick in"
            : n + " ticked: X -1, Y -5, A +1, B +5 (a held grip repeats)";
        nudgeNote.ForeColor = n > 0 && nudge.Linked ? Good : Dim;
    }

    // the spots the plugin docks the left hand on (src/dockpose.h)
    static bool Docked(int s) { return (s >= 3 && s <= 9) || (s >= 1000 && s < 2000); }
    static string SpotName(int s) {
        switch (s) {
        case 3: return "the slide / pump / bolt";
        case 4: return "the magazine";
        case 5: return "the grip handle";
        case 6: return "the drum";
        case 7: return "the insert point";
        case 8: return "the slide, from the left";
        case 9: return "rounds from the pouch";
        default: return s >= 1000 && s < 2000 ? "pair " + (s - 1000) + "'s landing spot" : "nothing";
        }
    }

    string PosesPath { get { return file.DataDir == null ? null : Path.Combine(file.DataDir, "dock_poses.json"); } }
    Dictionary<string, object> LoadPoses() {
        try { if (PosesPath != null && File.Exists(PosesPath)) { var o = Json.Obj(Json.Parse(File.ReadAllText(PosesPath))); if (o != null) return o; } } catch (Exception) { }
        return new Dictionary<string, object>();
    }
    static Dictionary<string, object> Child(Dictionary<string, object> d, string k) {
        object v;
        if (d.TryGetValue(k, out v) && v is Dictionary<string, object>) return (Dictionary<string, object>)v;
        var c = new Dictionary<string, object>();
        d[k] = c;
        return c;
    }

    void Poll() {
        if (file.Folder == null) {
            if ((DateTime.Now - lastFind).TotalMilliseconds > FIND_GAME_EVERY_MS) {
                lastFind = DateTime.Now;
                string f = ZoneFile.FindVillage();
                if (f != null) { try { file.Use(f); } catch (Exception) { } }
            }
            status.Text = "Village not found: set it once in Grip Zones (GAME FOLDER...)";
            status.ForeColor = Bad;
            return;
        }
        var live = file.ScriptAlive() ? file.ReadState() : null;
        if (live == null) {
            status.Text = "Village found, but the game is not running (or its zones script is not in it)";
            status.ForeColor = Warn;
            where.Text = "";
            wrist.Enabled = false;
            return;
        }
        gun = live.Gun; spot = live.Spot;
        status.Text = "connected · in hand: " + (gun > 0 ? Program.GunName(gun) : "no gun");
        status.ForeColor = Good;
        bool docked = gun > 0 && Docked(spot);
        where.Text = docked ? "LEFT HAND ON: " + SpotName(spot) : "the left hand is not docked: dock it (LG) to pose its wrist";
        where.ForeColor = docked ? Ink : Dim;
        if (wrist.Enabled != docked) wrist.Enabled = docked;
        string key = gun + ":" + spot;
        if (docked && key != loadedKey && !saveTimer.Enabled) {   // a new spot: its own numbers
            loadedKey = key;
            var mine = Child(Child(LoadPoses(), gun.ToString()), spot.ToString());
            syncing = true;
            wrist.Load(k => { object v; double n = mine.TryGetValue(k, out v) ? Json.Num(v, 0) : 0; return k.StartsWith("r") ? n : n * CM_PER_M; });
            syncing = false;
        }
    }

    void Save() {
        if (PosesPath == null || !Docked(spot) || gun <= 0 || loadedKey != gun + ":" + spot) return;
        var all = LoadPoses();
        var mine = Child(Child(all, gun.ToString()), spot.ToString());
        foreach (var kv in wrist.Values()) {
            double n = kv.Value is double ? (double)kv.Value : Convert.ToDouble(kv.Value);
            mine[kv.Key] = kv.Key.StartsWith("r") ? Math.Round(n, 2) : Math.Round(n / CM_PER_M, 4);
        }
        try { Directory.CreateDirectory(file.DataDir); File.WriteAllText(PosesPath, Json.Write(all)); }
        catch (Exception ex) { status.Text = "could not save: " + ex.Message; status.ForeColor = Bad; }
    }
}
