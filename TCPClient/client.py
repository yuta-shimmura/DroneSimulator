import json
import os
import socket
import threading
import tkinter as tk
from tkinter import scrolledtext, ttk

CFG_PATH = os.path.expanduser("~/.dronetcpclient.json")
MAX_DRONES = 8

THEMES = {
    "dark": {
        "bg":           "#1e1e2e",
        "panel":        "#2a2a3e",
        "input":        "#1a1a2a",
        "text":         "#e0e0e0",
        "sub":          "#888888",
        "accent":       "#5e81f4",
        "success":      "#4caf50",
        "success_h":    "#66bb6a",
        "success_p":    "#388e3c",
        "warning":      "#ff9800",
        "warning_h":    "#ffb74d",
        "warning_p":    "#f57c00",
        "error":        "#f44336",
        "btn_fg":       "#ffffff",
        "btn_disabled": "#555566",
        "row_even":     "#252535",
        "row_odd":      "#2a2a3e",
    },
    "light": {
        "bg":           "#f2f2f7",
        "panel":        "#ffffff",
        "input":        "#e8e8ed",
        "text":         "#1c1c1e",
        "sub":          "#6c6c70",
        "accent":       "#4a6cf7",
        "success":      "#34a853",
        "success_h":    "#4cba63",
        "success_p":    "#2d9047",
        "warning":      "#f29900",
        "warning_h":    "#ffad2e",
        "warning_p":    "#d4820a",
        "error":        "#d93025",
        "btn_fg":       "#ffffff",
        "btn_disabled": "#c0c0c8",
        "row_even":     "#f2f2f7",
        "row_odd":      "#ffffff",
    },
}

STRINGS = {
    "en": {
        "title":         "Drone TCP Client",
        "connection":    "Connection",
        "ip":            "IP",
        "port":          "Port",
        "drones":        "Drones",
        "col_no":        "#",
        "col_model":     "Model",
        "col_goal":      "Goal  X / Y / Z",
        "col_status":    "Status",
        "add_drone":     "+ Add Drone",
        "connect_all":   "Connect All",
        "disconnect_all":"Disconnect All",
        "start_all":     "Start All Autopilot",
        "remove":        "×",
        "not_connected": "Not connected",
        "connecting":    "Connecting…",
        "connected":     "Connected",
        "planning":      "Planning…",
        "flying":        "Flying {cur}/{total}",
        "arrived":       "Arrived",
        "failed":        "Failed",
        "disconnected":  "Disconnected",
        "auto_alt":      "Y=0 → auto altitude",
        "log":           "Log",
        "clear":         "Clear",
        "settings":      "Settings",
        "specs":         "Specs",
        "specs_title":   "Drone Specs",
        "spec_h_speed":  "Max H Speed",
        "spec_v_speed":  "Max V Speed",
        "spec_mass":     "Mass",
        "spec_battery":  "Battery",
        "spec_detect":   "Detect Range",
        "spec_wind":     "Wind Resist",
        "language":      "Language",
        "dark_mode":     "Dark Mode",
        "apply":         "Apply",
        "cancel":        "Cancel",
        "goal_err":      "Enter numeric values for Goal coordinates",
        "max_err":       f"Max {MAX_DRONES} drones",
    },
    "ja": {
        "title":         "Drone TCP Client",
        "connection":    "接続設定",
        "ip":            "IP",
        "port":          "Port",
        "drones":        "ドローン一覧",
        "col_no":        "#",
        "col_model":     "機体",
        "col_goal":      "Goal  X / Y / Z",
        "col_status":    "ステータス",
        "add_drone":     "+ ドローン追加",
        "connect_all":   "全機接続",
        "disconnect_all":"全機切断",
        "start_all":     "Autopilot 一斉開始",
        "remove":        "×",
        "not_connected": "未接続",
        "connecting":    "接続中…",
        "connected":     "接続済み",
        "planning":      "計算中…",
        "flying":        "飛行中 {cur}/{total}",
        "arrived":       "到着",
        "failed":        "失敗",
        "disconnected":  "切断",
        "auto_alt":      "Y=0 → 安全高度自動計算",
        "log":           "ログ",
        "clear":         "クリア",
        "settings":      "設定",
        "specs":         "スペック",
        "specs_title":   "機体スペック",
        "spec_h_speed":  "最高水平速度",
        "spec_v_speed":  "最高垂直速度",
        "spec_mass":     "重量",
        "spec_battery":  "バッテリー",
        "spec_detect":   "検知範囲",
        "spec_wind":     "風耐性",
        "language":      "言語",
        "dark_mode":     "ダークモード",
        "apply":         "適用",
        "cancel":        "キャンセル",
        "goal_err":      "Goal座標に数値を入力してください",
        "max_err":       f"最大{MAX_DRONES}機まで",
    },
}

DRONE_SPECS = {
    "Drone_TypeA": {
        "displayName":  "Drone Type A",
        "max_h_speed":  "19.0 m/s",
        "max_v_speed":  "6.0 m/s",
        "mass":         "0.895 kg",
        "battery":      "46 min",
        "detect_range": "50 m",
        "wind_resist":  "0.8 (strong)",
    },
    "Drone_TypeB": {
        "displayName":  "Drone Type B",
        "max_h_speed":  "16.0 m/s",
        "max_v_speed":  "5.0 m/s",
        "mass":         "0.249 kg",
        "battery":      "39 min",
        "detect_range": "30 m",
        "wind_resist":  "1.2 (weak)",
    },
    "Drone_TypeC": {
        "displayName":  "Drone Type C",
        "max_h_speed":  "12.0 m/s",
        "max_v_speed":  "4.0 m/s",
        "mass":         "3.5 kg",
        "battery":      "60 min",
        "detect_range": "60 m",
        "wind_resist":  "0.5 (very strong)",
    },
    "Drone_TypeD": {
        "displayName":  "Drone Type D",
        "max_h_speed":  "28.0 m/s",
        "max_v_speed":  "10.0 m/s",
        "mass":         "0.6 kg",
        "battery":      "20 min",
        "detect_range": "40 m",
        "wind_resist":  "1.5 (very weak)",
    },
}

STATUS_IDLE        = "idle"
STATUS_CONNECTING  = "connecting"
STATUS_CONNECTED   = "connected"
STATUS_PLANNING    = "planning"
STATUS_FLYING      = "flying"
STATUS_ARRIVED     = "arrived"
STATUS_FAILED      = "failed"
STATUS_DISCONNECTED = "disconnected"


def load_config():
    defaults = {"lang": "ja", "theme": "dark", "ip": "127.0.0.1", "port": "8080",
                "drones": [{"model": "Drone_TypeA", "gx": "50", "gy": "0", "gz": "50"}]}
    try:
        with open(CFG_PATH, "r") as f:
            loaded = json.load(f)
            return {**defaults, **loaded}
    except Exception:
        return defaults


def save_config(cfg):
    try:
        with open(CFG_PATH, "w") as f:
            json.dump(cfg, f, indent=2)
    except Exception:
        pass


class FlatButton:
    def __init__(self, parent, text, command,
                 normal, hover, press, fg,
                 font=("Arial", 12, "bold"),
                 padx=16, pady=5,
                 disabled_bg="#555566"):
        self._cmd         = command
        self._normal      = normal
        self._hover       = hover
        self._press       = press
        self._disabled_bg = disabled_bg
        self._enabled     = True
        self._w = tk.Label(parent, text=text, bg=normal, fg=fg,
                           font=font, padx=padx, pady=pady, cursor="hand2")
        self._w.bind("<Enter>",           self._on_enter)
        self._w.bind("<Leave>",           self._on_leave)
        self._w.bind("<ButtonPress-1>",   self._on_press)
        self._w.bind("<ButtonRelease-1>", self._on_release)

    def pack(self, **kw):      self._w.pack(**kw)
    def grid(self, **kw):      self._w.grid(**kw)
    def set_text(self, t):     self._w.config(text=t)
    def widget(self):          return self._w

    def set_colors(self, normal, hover, press):
        self._normal, self._hover, self._press = normal, hover, press
        if self._enabled: self._w.config(bg=normal)

    def enable(self):
        self._enabled = True
        self._w.config(bg=self._normal, cursor="hand2")

    def disable(self):
        self._enabled = False
        self._w.config(bg=self._disabled_bg, cursor="")

    def _on_enter(self, _):
        if self._enabled: self._w.config(bg=self._hover)

    def _on_leave(self, _):
        if self._enabled: self._w.config(bg=self._normal)

    def _on_press(self, _):
        if self._enabled: self._w.config(bg=self._press)

    def reset(self):
        if self._enabled: self._w.config(bg=self._normal)

    def _on_release(self, _):
        if self._enabled:
            self._w.config(bg=self._hover)
            self._cmd()


class DroneEntry:
    """1機分の状態・ウィジェット・ソケットを管理"""

    def __init__(self, idx, model="Drone_TypeA", gx="50", gy="0", gz="50"):
        self.idx      = idx
        self.model    = model
        self.gx, self.gy, self.gz = gx, gy, gz

        self.sock:      socket.socket | None = None
        self.connected: bool                 = False
        self.drone_id:  str | None           = None
        self.status:    str                  = STATUS_IDLE

        # ウィジェット参照（_build_row で設定）
        self.model_var:  tk.StringVar | None = None
        self.gx_var:     tk.StringVar | None = None
        self.gy_var:     tk.StringVar | None = None
        self.gz_var:     tk.StringVar | None = None
        self.status_lbl: tk.Label | None     = None
        self.row_frame:  tk.Frame | None     = None
        self.idx_lbl:    tk.Label | None     = None

    def send_json(self, obj: dict):
        if self.sock:
            try:
                self.sock.sendall((json.dumps(obj) + "\n").encode())
            except Exception:
                pass

    def close_socket(self):
        if self.sock:
            try:
                self.sock.close()
            except Exception:
                pass
            self.sock = None
        self.connected = False
        self.drone_id  = None


class DroneTCPClient:
    def __init__(self, root: tk.Tk):
        self.root = root
        self.root.resizable(False, False)

        cfg          = load_config()
        self._theme  = cfg.get("theme", "dark")
        self._lang   = cfg.get("lang", "ja")
        self._ip_str   = cfg.get("ip",   "127.0.0.1")
        self._port_str = cfg.get("port", "8080")
        self._log_buf  = ""
        self._frame    = None

        # ドローンエントリリスト（設定から復元）
        saved = cfg.get("drones", [{"model": "Drone_TypeA", "gx": "50", "gy": "0", "gz": "50"}])
        self._entries: list[DroneEntry] = [
            DroneEntry(i, d.get("model","Drone_TypeA"), d.get("gx","50"), d.get("gy","0"), d.get("gz","50"))
            for i, d in enumerate(saved)
        ]

        self._rebuild_ui()
        self._center()

    @property
    def _t(self): return THEMES[self._theme]

    @property
    def _s(self): return STRINGS[self._lang]

    # ── 設定保存 ────────────────────────────────────────────────────────

    def _save_cfg(self):
        drones_cfg = []
        for e in self._entries:
            drones_cfg.append({
                "model": e.model_var.get() if e.model_var else e.model,
                "gx":    e.gx_var.get()    if e.gx_var    else e.gx,
                "gy":    e.gy_var.get()    if e.gy_var    else e.gy,
                "gz":    e.gz_var.get()    if e.gz_var    else e.gz,
            })
        save_config({
            "lang": self._lang, "theme": self._theme,
            "ip":   self.ip_var.get() if hasattr(self, "ip_var") else self._ip_str,
            "port": self.port_var.get() if hasattr(self, "port_var") else self._port_str,
            "drones": drones_cfg,
        })

    # ── UI 構築 ──────────────────────────────────────────────────────────

    def _rebuild_ui(self):
        # 現在のウィジェット値を退避
        if hasattr(self, "ip_var"):
            self._ip_str   = self.ip_var.get()
            self._port_str = self.port_var.get()
        for e in self._entries:
            if e.model_var: e.model = e.model_var.get()
            if e.gx_var:    e.gx    = e.gx_var.get()
            if e.gy_var:    e.gy    = e.gy_var.get()
            if e.gz_var:    e.gz    = e.gz_var.get()
        if hasattr(self, "_log_box"):
            try:
                self._log_box.configure(state="normal")
                self._log_buf = self._log_box.get("1.0", "end-1c")
                self._log_box.configure(state="disabled")
            except Exception:
                pass

        if self._frame:
            self._frame.destroy()

        t = self._t
        self.root.title(self._s["title"])
        self.root.configure(bg=t["bg"])
        self._frame = tk.Frame(self.root, bg=t["bg"])
        self._frame.pack(fill="both", expand=True)

        self._build_title_row()
        self._build_connection_panel()
        self._build_drone_list()
        self._build_action_row()
        self._build_log_panel()

        if self._log_buf:
            self._log_box.configure(state="normal")
            self._log_box.insert("1.0", self._log_buf)
            self._log_box.see("end")
            self._log_box.configure(state="disabled")

        self.root.bind_all("<Button-1>", self._defocus_on_click, add="+")
        self.root.update_idletasks()
        w = self.root.winfo_reqwidth()
        h = self.root.winfo_reqheight()
        self.root.geometry(f"{w}x{h}+{self.root.winfo_x()}+{self.root.winfo_y()}")

    def _build_title_row(self):
        t = self._t; s = self._s
        row = tk.Frame(self._frame, bg=t["bg"])
        row.pack(fill="x", padx=16, pady=(16, 4))
        tk.Label(row, text=s["title"], bg=t["bg"], fg=t["text"],
                 font=("Arial", 18, "bold")).pack(side="left")
        self._settings_btn = FlatButton(row, s["settings"], self._open_settings,
                   normal=t["panel"], hover=t["input"], press=t["input"],
                   fg=t["sub"], font=("Arial", 14), padx=10, pady=4,
                   disabled_bg=t["btn_disabled"])
        self._settings_btn.pack(side="right")
        self._specs_btn = FlatButton(row, s["specs"], self._open_specs,
                   normal=t["panel"], hover=t["input"], press=t["input"],
                   fg=t["sub"], font=("Arial", 14), padx=10, pady=4,
                   disabled_bg=t["btn_disabled"])
        self._specs_btn.pack(side="right", padx=(0, 6))

    def _build_connection_panel(self):
        t = self._t; s = self._s
        f = self._panel(s["connection"])
        row = tk.Frame(f, bg=t["panel"])
        row.pack(fill="x", padx=10, pady=8)
        self._label(row, s["ip"], 4)
        self.ip_var = tk.StringVar(value=self._ip_str)
        self._entry(row, self.ip_var, 18)
        self._label(row, s["port"], 5, padx=(10, 0))
        self.port_var = tk.StringVar(value=self._port_str)
        self._entry(row, self.port_var, 6)

    def _build_drone_list(self):
        t = self._t; s = self._s
        outer = tk.Frame(self._frame, bg=t["bg"])
        outer.pack(fill="x", padx=16, pady=6)
        tk.Label(outer, text=s["drones"], bg=t["bg"], fg=t["sub"],
                 font=("Arial", 10)).pack(anchor="w")

        container = tk.Frame(outer, bg=t["panel"])
        container.pack(fill="x")

        # ヘッダー行
        hdr = tk.Frame(container, bg=t["panel"])
        hdr.pack(fill="x")
        for col, width, anchor, padx_l, text in [
            (0, 3,  "center", 6,  s["col_no"]),
            (1, 13, "w",      2,  s["col_model"]),
            (2, 26, "w",      16, s["col_goal"]),
            (3, 22, "w",      8,  s["col_status"]),
            (4, 3,  "w",      2,  ""),
        ]:
            tk.Label(hdr, text=text, bg=t["panel"], fg=t["sub"],
                     font=("Arial", 9), width=width, anchor=anchor).grid(
                row=0, column=col, padx=(padx_l, 2), pady=4, sticky="w")

        tk.Frame(container, bg=t["sub"], height=1).pack(fill="x")

        self._list_container = container
        self._list_rows = tk.Frame(container, bg=t["panel"])
        self._list_rows.pack(fill="x")

        for e in self._entries:
            self._build_row(e)

        # 追加ボタン
        add_row = tk.Frame(container, bg=t["panel"])
        add_row.pack(fill="x", pady=4)
        self._add_btn = FlatButton(add_row, s["add_drone"], self._add_drone,
                   normal=t["panel"], hover=t["input"], press=t["input"],
                   fg=t["accent"], font=("Arial", 10, "bold"),
                   padx=12, pady=3,
                   disabled_bg=t["btn_disabled"])
        self._add_btn.pack(side="left", padx=8)
        tk.Label(add_row, text=s["auto_alt"],
                 bg=t["panel"], fg=t["sub"], font=("Arial", 9)).pack(side="left", padx=(24, 8))
        self._add_btn_frame = add_row
        self._update_add_btn()

    def _build_row(self, e: DroneEntry):
        t = self._t
        bg = t["row_even"] if e.idx % 2 == 0 else t["row_odd"]
        row = tk.Frame(self._list_rows, bg=bg)
        row.pack(fill="x")
        e.row_frame = row

        # # 列
        e.idx_lbl = tk.Label(row, text=str(e.idx + 1), bg=bg, fg=t["sub"],
                             font=("Arial", 10), width=3)
        e.idx_lbl.grid(row=0, column=0, padx=(6, 2), pady=4)

        # 機体選択
        e.model_var = tk.StringVar(value=e.model)
        _cb = ttk.Combobox(row, textvariable=e.model_var,
                           values=["Drone_TypeA", "Drone_TypeB", "Drone_TypeC", "Drone_TypeD"],
                           font=("Arial", 10), width=11, state="readonly")
        _cb.bind("<<ComboboxSelected>>", lambda ev, c=_cb: c.selection_clear())
        _cb.grid(row=0, column=1, padx=2, pady=4)

        # Goal X/Y/Z
        goal_frame = tk.Frame(row, bg=bg)
        goal_frame.grid(row=0, column=2, padx=(8, 2), pady=4)
        e.gx_var = tk.StringVar(value=e.gx)
        e.gy_var = tk.StringVar(value=e.gy)
        e.gz_var = tk.StringVar(value=e.gz)
        for i, (var, placeholder) in enumerate([(e.gx_var, "X"), (e.gy_var, "Y"), (e.gz_var, "Z")]):
            if i > 0:
                tk.Label(goal_frame, text="/", bg=bg, fg=t["sub"],
                         font=("Arial", 10)).pack(side="left")
            tk.Entry(goal_frame, textvariable=var,
                     bg=t["input"], fg=t["text"],
                     insertbackground=t["text"],
                     font=("Arial", 10), relief="flat",
                     width=6).pack(side="left", padx=1)

        # ステータス
        e.status_lbl = tk.Label(row, text=self._s["not_connected"],
                                 bg=bg, fg=t["sub"],
                                 font=("Arial", 10), width=22, anchor="w")
        e.status_lbl.grid(row=0, column=3, padx=4, pady=4)

        # 削除ボタン
        def make_remove(entry):
            return lambda: self._remove_drone(entry)

        FlatButton(row, self._s["remove"], make_remove(e),
                   normal=bg, hover=t["error"], press=t["error"],
                   fg=t["sub"], font=("Arial", 11, "bold"),
                   padx=6, pady=2,
                   disabled_bg=bg).grid(row=0, column=4, padx=(2, 6))

        self._refresh_row_status(e)

    def _build_action_row(self):
        t = self._t; s = self._s
        row = tk.Frame(self._frame, bg=t["bg"])
        row.pack(fill="x", padx=16, pady=(4, 8))

        self._conn_all_btn = FlatButton(row, s["connect_all"], self._connect_all,
                   normal=t["success"], hover=t["success_h"], press=t["success_p"],
                   fg=t["btn_fg"], font=("Arial", 12, "bold"),
                   padx=14, pady=6, disabled_bg=t["btn_disabled"])
        self._conn_all_btn.pack(side="left", padx=(0, 6))

        self._disc_all_btn = FlatButton(row, s["disconnect_all"], self._disconnect_all,
                   normal=t["warning"], hover=t["warning_h"], press=t["warning_p"],
                   fg=t["btn_fg"], font=("Arial", 12, "bold"),
                   padx=14, pady=6, disabled_bg=t["btn_disabled"])
        self._disc_all_btn.pack(side="left", padx=6)

        self._start_all_btn = FlatButton(row, s["start_all"], self._start_all,
                   normal=t["accent"], hover="#7090ff", press="#4060d0",
                   fg=t["btn_fg"], font=("Arial", 12, "bold"),
                   padx=14, pady=6, disabled_bg=t["btn_disabled"])
        self._start_all_btn.pack(side="left", padx=6)

    def _build_log_panel(self):
        t = self._t; s = self._s
        f = self._panel(s["log"])
        header = tk.Frame(f, bg=t["panel"])
        header.pack(fill="x", padx=8, pady=(6, 0))
        tk.Label(header, bg=t["panel"]).pack(side="left", expand=True)
        FlatButton(header, s["clear"], self._clear_log,
                   normal=t["panel"], hover=t["input"], press=t["input"],
                   fg=t["sub"], font=("Arial", 9, "bold"),
                   padx=8, pady=2,
                   disabled_bg=t["btn_disabled"]).pack(side="right")
        self._log_box = scrolledtext.ScrolledText(
            f, bg=t["input"], fg=t["text"],
            font=("Courier", 10), height=8, relief="flat", state="disabled")
        self._log_box.pack(fill="both", padx=8, pady=(2, 8))

    # ── ウィジェットヘルパー ─────────────────────────────────────────────

    def _panel(self, title: str) -> tk.Frame:
        t = self._t
        outer = tk.Frame(self._frame, bg=t["bg"])
        outer.pack(fill="x", padx=16, pady=6)
        tk.Label(outer, text=title, bg=t["bg"], fg=t["sub"],
                 font=("Arial", 10)).pack(anchor="w")
        inner = tk.Frame(outer, bg=t["panel"])
        inner.pack(fill="x")
        return inner

    def _label(self, parent, text, width, padx=(0, 0)):
        t = self._t
        tk.Label(parent, text=text, bg=t["panel"], fg=t["sub"],
                 font=("Arial", 11), width=width).pack(side="left", padx=padx)

    def _entry(self, parent, var, width):
        t = self._t
        tk.Entry(parent, textvariable=var,
                 bg=t["input"], fg=t["text"],
                 insertbackground=t["text"],
                 font=("Arial", 12), relief="flat",
                 width=width).pack(side="left", padx=4)

    def _defocus_on_click(self, event):
        if event.widget.winfo_toplevel() != self.root:
            return
        if not isinstance(event.widget, (tk.Entry, ttk.Combobox, tk.Text, scrolledtext.ScrolledText)):
            self.root.focus_set()

    def _center(self):
        self.root.withdraw()
        self.root.update_idletasks()
        w = self.root.winfo_reqwidth()
        h = self.root.winfo_reqheight()
        sw, sh = self.root.winfo_screenwidth(), self.root.winfo_screenheight()
        self.root.geometry(f"{w}x{h}+{(sw - w) // 2}+{(sh - h) // 4}")
        self.root.deiconify()

    # ── ドローン追加/削除 ────────────────────────────────────────────────

    def _update_add_btn(self):
        if len(self._entries) >= MAX_DRONES:
            self._add_btn.set_text(self._s["max_err"])
            self._add_btn.disable()
        else:
            self._add_btn.set_text(self._s["add_drone"])
            self._add_btn.enable()

    def _add_drone(self):
        if len(self._entries) >= MAX_DRONES:
            return
        e = DroneEntry(len(self._entries))
        self._entries.append(e)
        self._build_row(e)
        self._update_add_btn()
        self._save_cfg()
        self.root.update_idletasks()
        w = self.root.winfo_reqwidth()
        h = self.root.winfo_reqheight()
        self.root.geometry(f"{w}x{h}+{self.root.winfo_x()}+{self.root.winfo_y()}")

    def _remove_drone(self, e: DroneEntry):
        if e.status in (STATUS_FLYING, STATUS_PLANNING):
            return
        e.close_socket()
        if e.row_frame:
            e.row_frame.destroy()
        self._entries.remove(e)
        for i, entry in enumerate(self._entries):
            entry.idx = i
            if entry.idx_lbl:
                entry.idx_lbl.config(text=str(i + 1))
        self._update_add_btn()
        self._save_cfg()
        self.root.update_idletasks()
        w = self.root.winfo_reqwidth()
        h = self.root.winfo_reqheight()
        self.root.geometry(f"{w}x{h}+{self.root.winfo_x()}+{self.root.winfo_y()}")

    # ── 接続 ─────────────────────────────────────────────────────────────

    def _connect_all(self):
        host = self.ip_var.get().strip()
        try:
            port = int(self.port_var.get().strip())
        except ValueError:
            self._log("Invalid port")
            return
        for e in self._entries:
            if e.status in (STATUS_IDLE, STATUS_DISCONNECTED):
                self._connect_entry(e, host, port)

    def _connect_entry(self, e: DroneEntry, host: str, port: int):
        self._set_entry_status(e, STATUS_CONNECTING)
        model = e.model_var.get() if e.model_var else e.model

        def _worker():
            try:
                sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                sock.settimeout(5)
                sock.connect((host, port))
                sock.settimeout(None)
                e.sock      = sock
                e.connected = True
                e.send_json({"type": "connect", "modelId": model})
                self.root.after(0, lambda: self._log(f"#{e.idx+1} → connect {host}:{port}"))
                threading.Thread(target=self._recv_loop, args=(e,), daemon=True).start()
            except Exception as ex:
                e.close_socket()
                self.root.after(0, lambda: self._set_entry_status(e, STATUS_DISCONNECTED))
                self.root.after(0, lambda: self._log(f"#{e.idx+1} connection failed: {ex}"))

        threading.Thread(target=_worker, daemon=True).start()

    def _disconnect_all(self):
        for e in self._entries:
            self._disconnect_entry(e)

    def _disconnect_entry(self, e: DroneEntry):
        if not e.connected:
            return
        e.send_json({"type": "disconnect"})
        e.close_socket()
        self._set_entry_status(e, STATUS_DISCONNECTED)
        self._log(f"#{e.idx+1} disconnected")

    # ── 受信ループ ────────────────────────────────────────────────────────

    def _recv_loop(self, e: DroneEntry):
        buf = ""
        try:
            while e.connected:
                chunk = e.sock.recv(4096).decode("utf-8")
                if not chunk:
                    break
                buf += chunk
                while "\n" in buf:
                    line, buf = buf.split("\n", 1)
                    line = line.strip()
                    if line:
                        self.root.after(0, lambda l=line, entry=e: self._handle(entry, l))
        except Exception:
            pass
        self.root.after(0, lambda: self._on_disconnect(e))

    def _on_disconnect(self, e: DroneEntry):
        if not e.connected:
            return
        e.close_socket()
        self._set_entry_status(e, STATUS_DISCONNECTED)
        self._log(f"#{e.idx+1} disconnected")

    def _handle(self, e: DroneEntry, line: str):
        try:
            msg = json.loads(line)
        except Exception:
            if '"type":"state"' not in line:
                self._log(f"#{e.idx+1} ← {line}")
            return

        msg_type = msg.get("type", "")

        if msg_type == "connected":
            e.drone_id = msg.get("droneId", "")
            spawn_index = msg.get("spawnIndex", None)
            if spawn_index is not None and e.idx_lbl:
                e.idx_lbl.config(text=str(spawn_index + 1))
            self._set_entry_status(e, STATUS_CONNECTED)
            self._log(f"#{(spawn_index + 1) if spawn_index is not None else e.idx+1} ← connected: {e.drone_id}")

        elif msg_type == "autopilot_status":
            status = msg.get("status", "")
            if status == "planning":
                self._set_entry_status(e, STATUS_PLANNING)
            elif status == "flying":
                self._set_entry_status(e, STATUS_FLYING)
            elif status == "arrived":
                self._set_entry_status(e, STATUS_ARRIVED)
            elif status == "failed":
                self._set_entry_status(e, STATUS_FAILED)
            self._log(f"#{e.idx+1} ← autopilot_status: {status}")

        elif msg_type == "state":
            ap = msg.get("autopilot", {})
            if ap.get("active") and e.status == STATUS_FLYING:
                cur   = ap.get("currentWaypoint", 0)
                total = ap.get("totalWaypoints", 0)
                self._update_status_text(e,
                    self._s["flying"].format(cur=cur, total=total),
                    self._t["accent"])

    # ── Autopilot 送信 ────────────────────────────────────────────────────

    def _start_all(self):
        s = self._s
        for e in self._entries:
            if not e.connected or not e.drone_id:
                continue
            try:
                goal = {
                    "x": float(e.gx_var.get()),
                    "y": float(e.gy_var.get()),
                    "z": float(e.gz_var.get()),
                }
            except ValueError:
                self._log(f"#{e.idx+1} {s['goal_err']}")
                continue
            e.send_json({"type": "autopilot", "droneId": e.drone_id, "goal": goal})
            self._log(f"#{e.idx+1} → autopilot goal=({goal['x']}, {goal['y']}, {goal['z']})")

    # ── ステータス表示 ─────────────────────────────────────────────────────

    def _set_entry_status(self, e: DroneEntry, status: str):
        e.status = status
        self._refresh_row_status(e)

    def _refresh_row_status(self, e: DroneEntry):
        if e.status_lbl is None:
            return
        t = self._t; s = self._s
        text_map = {
            STATUS_IDLE:         (s["not_connected"], t["sub"]),
            STATUS_CONNECTING:   (s["connecting"],    t["warning"]),
            STATUS_CONNECTED:    (s["connected"],     t["success"]),
            STATUS_PLANNING:     (s["planning"],      t["warning"]),
            STATUS_FLYING:       (s["flying"].format(cur="?", total="?"), t["accent"]),
            STATUS_ARRIVED:      (s["arrived"],       t["success"]),
            STATUS_FAILED:       (s["failed"],        t["error"]),
            STATUS_DISCONNECTED: (s["disconnected"],  t["error"]),
        }
        text, color = text_map.get(e.status, (e.status, t["text"]))
        e.status_lbl.config(text=text, fg=color)

    def _update_status_text(self, e: DroneEntry, text: str, color: str):
        if e.status_lbl:
            e.status_lbl.config(text=text, fg=color)

    # ── ログ ─────────────────────────────────────────────────────────────

    def _clear_log(self):
        self._log_buf = ""
        self._log_box.configure(state="normal")
        self._log_box.delete("1.0", "end")
        self._log_box.configure(state="disabled")

    def _log(self, text: str):
        self._log_box.configure(state="normal")
        self._log_box.insert("end", text + "\n")
        self._log_box.see("end")
        self._log_box.configure(state="disabled")

    # ── 設定ダイアログ ────────────────────────────────────────────────────

    def _open_specs(self):
        t = self._t; s = self._s
        dlg = tk.Toplevel(self.root)
        dlg.title(s["specs_title"])
        dlg.configure(bg=t["bg"])
        dlg.resizable(False, False)
        dlg.withdraw()
        dlg.transient(self.root)
        dlg.grab_set()
        dlg.bind("<Destroy>", lambda e: self._specs_btn.reset() if e.widget is dlg else None)

        headers = ["", "Type A", "Type B", "Type C", "Type D"]
        rows_data = [
            (s["spec_h_speed"], "max_h_speed"),
            (s["spec_v_speed"], "max_v_speed"),
            (s["spec_mass"],    "mass"),
            (s["spec_battery"], "battery"),
            (s["spec_detect"],  "detect_range"),
            (s["spec_wind"],    "wind_resist"),
        ]
        types = ["Drone_TypeA", "Drone_TypeB", "Drone_TypeC", "Drone_TypeD"]

        frame = tk.Frame(dlg, bg=t["bg"])
        frame.pack(padx=20, pady=16)

        col_w = 100
        for c, h in enumerate(headers):
            anchor = "w" if c == 0 else "center"
            width  = 14 if c == 0 else 10
            tk.Label(frame, text=h, bg=t["panel"], fg=t["sub"],
                     font=("Arial", 10, "bold"), width=width, anchor=anchor,
                     padx=6, pady=4).grid(row=0, column=c, padx=1, pady=1, sticky="nsew")

        for r, (label, key) in enumerate(rows_data, start=1):
            bg = t["panel"] if r % 2 == 0 else t["input"]
            tk.Label(frame, text=label, bg=bg, fg=t["sub"],
                     font=("Arial", 10), width=14, anchor="w",
                     padx=6, pady=4).grid(row=r, column=0, padx=1, pady=1, sticky="nsew")
            for c, model_id in enumerate(types, start=1):
                val = DRONE_SPECS.get(model_id, {}).get(key, "-")
                tk.Label(frame, text=val, bg=bg, fg=t["text"],
                         font=("Arial", 10), width=10, anchor="center",
                         padx=6, pady=4).grid(row=r, column=c, padx=1, pady=1, sticky="nsew")

        tk.Button(dlg, text=s["cancel"], command=dlg.destroy,
                  bg=t["accent"], fg="#0a0a0a", relief=tk.FLAT,
                  padx=16, pady=5, font=("Helvetica", 12, "bold"),
                  cursor="hand2").pack(pady=(0, 16))

        dlg.update_idletasks()
        w  = dlg.winfo_reqwidth()
        h  = dlg.winfo_reqheight()
        mx = self.root.winfo_x() + (self.root.winfo_width()  - w) // 2
        my = self.root.winfo_y() + (self.root.winfo_height() - h) // 2
        dlg.geometry(f"{w}x{h}+{mx}+{my}")
        dlg.deiconify()

    def _open_settings(self):
        t = self._t; s = self._s
        dlg = tk.Toplevel(self.root)
        dlg.title(s["settings"])
        dlg.configure(bg=t["bg"])
        dlg.resizable(False, False)
        dlg.withdraw()
        dlg.transient(self.root)
        dlg.grab_set()
        dlg.bind("<Destroy>", lambda e: self._settings_btn.reset() if e.widget is dlg else None)

        pad = {"padx": 24, "pady": 10}
        tk.Label(dlg, text=s["language"], bg=t["bg"], fg=t["text"],
                 font=("Arial", 13)).grid(row=0, column=0, sticky="w", **pad)
        lang_var = tk.StringVar(value=self._lang)
        lf = tk.Frame(dlg, bg=t["bg"])
        lf.grid(row=0, column=1, sticky="w", **pad)
        for val, lbl in (("en", "English"), ("ja", "日本語")):
            tk.Radiobutton(lf, text=lbl, variable=lang_var, value=val,
                           bg=t["bg"], fg=t["text"], selectcolor=t["panel"],
                           activebackground=t["bg"],
                           font=("Arial", 12)).pack(side="left", padx=8)

        tk.Label(dlg, text=s["dark_mode"], bg=t["bg"], fg=t["text"],
                 font=("Arial", 13)).grid(row=1, column=0, sticky="w", **pad)
        dark_var = tk.BooleanVar(value=(self._theme == "dark"))
        tk.Checkbutton(dlg, variable=dark_var,
                       bg=t["bg"], fg=t["text"],
                       selectcolor=t["panel"],
                       activebackground=t["bg"]).grid(row=1, column=1, sticky="w", **pad)

        bf = tk.Frame(dlg, bg=t["bg"])
        bf.grid(row=2, column=0, columnspan=2, pady=(4, 20))

        def apply():
            self._lang  = lang_var.get()
            self._theme = "dark" if dark_var.get() else "light"
            save_config({"lang": self._lang, "theme": self._theme})
            dlg.destroy()
            self._rebuild_ui()

        tk.Button(bf, text=s["apply"], command=apply,
                  bg=t["accent"], fg="#0a0a0a", relief=tk.FLAT,
                  padx=16, pady=5, font=("Helvetica", 12, "bold"),
                  cursor="hand2").pack(side=tk.LEFT, padx=6)
        tk.Button(bf, text=s["cancel"], command=dlg.destroy,
                  bg=t["accent"], fg="#0a0a0a", relief=tk.FLAT,
                  padx=16, pady=5, font=("Helvetica", 12, "bold"),
                  cursor="hand2").pack(side=tk.LEFT, padx=6)

        dlg.update_idletasks()
        w  = dlg.winfo_reqwidth()
        h  = dlg.winfo_reqheight()
        mx = self.root.winfo_x() + (self.root.winfo_width()  - w) // 2
        my = self.root.winfo_y() + (self.root.winfo_height() - h) // 2
        dlg.geometry(f"{w}x{h}+{mx}+{my}")
        dlg.deiconify()


def main():
    root = tk.Tk()
    root.withdraw()
    DroneTCPClient(root)
    root.mainloop()


if __name__ == "__main__":
    main()
