#!/usr/bin/env python3
"""DroneSimulator Flight Log Visualizer"""

import os
import json
from datetime import datetime
import tkinter as tk
from tkinter import ttk, filedialog, messagebox
import pandas as pd

# matplotlib is imported lazily to speed up startup.
# It is loaded synchronously on the main thread while the file picker is open.
matplotlib        = None
plt               = None
FigureCanvasTkAgg = None
_ThemedToolbar    = None
_mpl_loaded       = False

# Dock icon guard: block macOS from resetting the icon during canvas.draw().
# canvas.draw() triggers an AppKit notification that calls setApplicationIconImage:
# with the bundle default (Python.framework icon). We swizzle the method to ignore
# all calls except our own _restore_dock_icon().
_icon_change_allowed = False
_icon_guard_refs     = None   # keeps ctypes IMP callbacks alive

def _setup_icon_guard():
    """Swizzle NSApplication.setApplicationIconImage: to block unauthorized resets."""
    global _icon_guard_refs
    import sys
    if sys.platform != "darwin":
        return
    try:
        import ctypes, ctypes.util
        lib = ctypes.cdll.LoadLibrary(ctypes.util.find_library("objc"))
        lib.objc_getClass.restype              = ctypes.c_void_p
        lib.objc_getClass.argtypes             = [ctypes.c_char_p]
        lib.sel_registerName.restype           = ctypes.c_void_p
        lib.sel_registerName.argtypes          = [ctypes.c_char_p]
        lib.class_getInstanceMethod.restype    = ctypes.c_void_p
        lib.class_getInstanceMethod.argtypes   = [ctypes.c_void_p, ctypes.c_void_p]
        lib.method_getImplementation.restype   = ctypes.c_void_p
        lib.method_getImplementation.argtypes  = [ctypes.c_void_p]
        lib.class_replaceMethod.restype        = ctypes.c_void_p
        lib.class_replaceMethod.argtypes       = [ctypes.c_void_p, ctypes.c_void_p,
                                                   ctypes.c_void_p, ctypes.c_char_p]
        cls  = lib.objc_getClass(b"NSApplication")
        sel  = lib.sel_registerName(b"setApplicationIconImage:")
        meth = lib.class_getInstanceMethod(cls, sel)
        orig_ptr = lib.method_getImplementation(meth)
        IMP  = ctypes.CFUNCTYPE(None, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p)
        orig_imp = IMP(orig_ptr)
        def guarded(self_p, sel_p, img_p):
            if _icon_change_allowed:
                orig_imp(self_p, sel_p, img_p)
        guard_imp = IMP(guarded)
        _icon_guard_refs = (guard_imp, orig_imp)   # prevent GC
        lib.class_replaceMethod(cls, sel, guard_imp, b"v@:@")
    except Exception:
        pass

def _ensure_matplotlib():
    global matplotlib, plt, FigureCanvasTkAgg, _ThemedToolbar, _mpl_loaded
    if _mpl_loaded:
        return
    # Override PyInstaller's per-run temp MPLCONFIGDIR so the font cache persists across launches
    import os as _os
    _os.environ["MPLCONFIGDIR"] = _os.path.expanduser("~/.dronelogvisualizer_mpl")
    import matplotlib as _mpl
    _mpl.use("TkAgg")
    matplotlib = _mpl
    import matplotlib.pyplot as _plt
    plt = _plt
    from matplotlib.backends.backend_tkagg import (
        FigureCanvasTkAgg as _FCA,
        NavigationToolbar2Tk as _NT,
    )
    FigureCanvasTkAgg = _FCA
    from mpl_toolkits.mplot3d import Axes3D  # noqa: F401
    class _Toolbar(_ToolbarMixin, _NT):
        pass
    _ThemedToolbar = _Toolbar
    _mpl_loaded = True

APP_TITLE  = "DroneSimulator Log Visualizer"
COLORS     = ["#14d1eb", "#f0a500", "#7ec850", "#e05555", "#a070e0", "#60c0c0", "#ee77aa", "#d4c000"]
CFG_PATH   = os.path.expanduser("~/.dronelogvisualizer.json")

THEMES = {
    "dark": {
        "bg":      "#1e1e1e",
        "bg2":     "#252525",
        "bg3":     "#2a2a2a",
        "fg":      "#e0e0e0",
        "fg2":     "#888888",
        "accent":  "#14d1eb",
        "tab_sel": "#333333",
        "tab_unsel": "#aaaaaa",
        "tab_unsel_fg": "#000000",
        "btn_fg":  "#0a0a0a",
        "sel_bg":  "#14d1eb",
        "sel_fg":  "#0a0a0a",
    },
    "light": {
        "bg":      "#f2f2f7",
        "bg2":     "#ffffff",
        "bg3":     "#dde1e7",
        "fg":      "#1c1c1e",
        "fg2":     "#6c6c70",
        "accent":  "#00aadd",
        "tab_sel": "#ffffff",
        "tab_unsel": "#c8cdd6",
        "tab_unsel_fg": "#3a3a3c",
        "btn_fg":  "#0a0a0a",
        "sel_bg":  "#00aadd",
        "sel_fg":  "#0a0a0a",
    },
}

STRINGS = {
    "en": {
        "open":          "Open Log(s)",
        "clear":         "Clear All",
        "no_log":        "No log loaded.",
        "loaded":        "Loaded: ",
        "tab_ts":        "  Time Series  ",
        "tab_3d":        "  3D Path  ",
        "tab_info":      "  Info  ",
        "settings":      "Settings",
        "language":      "Language",
        "dark_mode":     "Dark Mode",
        "log_folder":    "Import Folder",
        "export_folder": "Export Folder",
        "browse":        "Browse...",
        "apply":         "Apply",
        "cancel":        "Cancel",
        "close":         "Close",
        "pick_title":    "Select Log File(s)",
        "col_name":      "File Name",
        "col_size":      "Size",
        "col_modified":  "Modified",
        "open_sel":      "Open",
        "no_folder":     "Log folder not set. Configure in Settings (⚙).",
        "folder_empty":  "No CSV files found in the selected folder.",
        "alt":           "Altitude",
        "spd":           "Speed",
        "force_y":       "Wind Force (H)",
        "bld_effect":    "Building Effect",
        "time_s":        "Time (s)",
        "alt_m":         "Altitude\n(m)",
        "spd_ms":        "Speed\n(m/s)",
        "force_n":       "Force\n(N)",
        "3d_title":      "3D Flight Path   ○ start   × end   ▲ collision",
        "x_m":           "X (m)",
        "z_m":           "Z (m)",
        "alt_m_ax":      "Altitude (m)",
        "duration":      "Duration",
        "samples":       "Samples",
        "collisions":    "Collisions",
        "max_alt":       "Max Altitude",
        "max_spd":       "Max Speed",
        "weather_fy":    "Weather Fy",
        "thermal_y":     "Thermal Y",
        "downdraft_y":   "Downdraft Y",
        "active_layers": "Active Layers",
        "load_err":      "Load Error",
        "thermal":       "thermal",
        "downdraft":     "downdraft",
        "collision":     "collision",
        "open_finder":   "Open in Finder",
        "help":          "Help",
        "help_title":    "How to Use",
        "help_body":     (
            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n"
            " Setup\n"
            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n"
            " Click [Settings] to configure:\n"
            "   · Log Folder    — folder scanned for CSVs\n"
            "   · Export Folder — default save location\n"
            "                    for graph images\n"
            "   · Language      — English / Japanese\n"
            "   · Dark Mode     — toggle light/dark theme\n"
            " Click [Apply] to save.\n"
            "\n"
            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n"
            " Opening Logs\n"
            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n"
            " · Click [Open Log(s)] to browse CSV files\n"
            "   in the configured folder.\n"
            " · Select one or more files:\n"
            "     Ctrl+click    toggle individual files\n"
            "     Shift+click   select a range\n"
            "     Shift+↑↓      extend selection with keyboard\n"
            " · Double-click or press Enter to open.\n"
            " · [Open in Finder] opens the log folder.\n"
            " · Column headers are clickable to sort.\n"
            " · Click [Clear All] to remove all loaded logs.\n"
            "\n"
            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n"
            " Tabs\n"
            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n"
            " Time Series\n"
            "   Altitude and Speed over time.\n"
            "   If building effect data exists, a third\n"
            "   graph shows Thermal / Downdraft forces.\n"
            "   Red dotted lines = collision events.\n"
            "\n"
            " 3D Path\n"
            "   3D flight trajectory.\n"
            "   ○ = start,  × = end,  ▲ = collision\n"
            "\n"
            " Info\n"
            "   Summary: duration, max altitude,\n"
            "   max speed, weather force range,\n"
            "   active layers, etc.\n"
            "\n"
            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n"
            " Tips\n"
            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n"
            " · Load multiple CSVs to compare them\n"
            "   side by side in different colors.\n"
            " · TCP mode logs (CSV with drone_id column)\n"
            "   are auto-split per drone in different\n"
            "   colors.\n"
            " · Use the matplotlib toolbar at the\n"
            "   bottom to zoom, pan, and save graphs.\n"
        ),
    },
    "ja": {
        "open":          "ログを開く",
        "clear":         "クリア",
        "no_log":        "ログが読み込まれていません",
        "loaded":        "読込済み: ",
        "tab_ts":        "  時系列  ",
        "tab_3d":        "  3D経路  ",
        "tab_info":      "  情報  ",
        "settings":      "設定",
        "language":      "言語",
        "dark_mode":     "ダークモード",
        "log_folder":    "インポートフォルダ",
        "export_folder": "エクスポートフォルダ",
        "browse":        "参照...",
        "apply":         "適用",
        "cancel":        "キャンセル",
        "close":         "閉じる",
        "pick_title":    "ログファイルを選択",
        "col_name":      "ファイル名",
        "col_size":      "サイズ",
        "col_modified":  "更新日時",
        "open_sel":      "開く",
        "no_folder":     "ログフォルダが未設定です。設定（⚙）から指定してください。",
        "folder_empty":  "指定フォルダにCSVファイルがありません。",
        "alt":           "高度",
        "spd":           "速度",
        "force_y":       "水平風力",
        "bld_effect":    "建物効果",
        "time_s":        "時刻 (s)",
        "alt_m":         "高度\n(m)",
        "spd_ms":        "速度\n(m/s)",
        "force_n":       "力\n(N)",
        "3d_title":      "3D飛行経路   ○ 開始   × 終了   ▲ 衝突",
        "x_m":           "X (m)",
        "z_m":           "Z (m)",
        "alt_m_ax":      "高度 (m)",
        "duration":      "飛行時間",
        "samples":       "サンプル数",
        "collisions":    "衝突回数",
        "max_alt":       "最大高度",
        "max_spd":       "最大速度",
        "weather_fy":    "気象力 Y",
        "thermal_y":     "上昇気流 Y",
        "downdraft_y":   "下降気流 Y",
        "active_layers": "アクティブレイヤー",
        "load_err":      "読込エラー",
        "thermal":       "上昇気流",
        "downdraft":     "下降気流",
        "collision":     "衝突",
        "open_finder":   "Finderで開く",
        "help":          "ヘルプ",
        "help_title":    "操作方法",
        "help_body":     (
            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n"
            " 初期設定\n"
            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n"
            " [設定] をクリックして各項目を設定する:\n"
            "   · ログフォルダ        CSV を検索するフォルダ\n"
            "   · エクスポートフォルダ  グラフ画像の保存先\n"
            "   · 言語              English / 日本語\n"
            "   · ダークモード        テーマの切替\n"
            " [適用] をクリックして保存。\n"
            "\n"
            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n"
            " ログを開く\n"
            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n"
            " · [ログを開く] をクリックすると\n"
            "   設定フォルダ内の CSV 一覧が表示される。\n"
            " · ファイルの選択方法:\n"
            "     Ctrl+クリック    個別にトグル選択\n"
            "     Shift+クリック   範囲選択\n"
            "     Shift+↑↓        キーボードで範囲拡張\n"
            " · ダブルクリックまたは Enter でも開ける。\n"
            " · [Finderで開く] でログフォルダを開ける。\n"
            " · 列ヘッダーをクリックすると並び替え可能。\n"
            " · [クリア] で読み込み済みログを全て削除。\n"
            "\n"
            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n"
            " 各タブの説明\n"
            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n"
            " 時系列\n"
            "   高度・速度の時系列グラフ。\n"
            "   建物効果データがある場合は\n"
            "   Thermal / Downdraft の3グラフ目を追加表示。\n"
            "   赤点線 = 衝突イベント。\n"
            "\n"
            " 3D経路\n"
            "   3次元飛行軌跡。\n"
            "   ○ = 開始,  × = 終了,  ▲ = 衝突\n"
            "\n"
            " 情報\n"
            "   飛行時間・最大高度・最大速度・\n"
            "   気象力範囲・アクティブレイヤーなどのサマリー。\n"
            "\n"
            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n"
            " Tips\n"
            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n"
            " · 複数の CSV を同時に開くと色別で比較できる。\n"
            " · TCPモードの多機体ログ（drone_id列付きCSV）は\n"
            "   機体ごとに自動分割・色別表示される。\n"
            " · グラフ下部の toolbar でズーム・\n"
            "   パン・画像保存が可能。\n"
        ),
    },
}


# ── Config ────────────────────────────────────────────────────────────────────

def load_config():
    defaults = {"lang": "en", "theme": "dark", "log_folder": "", "export_folder": ""}
    try:
        with open(CFG_PATH, "r") as f:
            return {**defaults, **json.load(f)}
    except Exception:
        return defaults


def save_config(cfg):
    try:
        with open(CFG_PATH, "w") as f:
            json.dump(cfg, f, indent=2)
    except Exception:
        pass


# ── CSV loading ───────────────────────────────────────────────────────────────

def load_csv(path):
    df = pd.read_csv(path, encoding="utf-8-sig")
    df.rename(columns={
        "force_x": "weather_force_x",
        "force_y": "weather_force_y",
        "force_z": "weather_force_z",
    }, inplace=True)
    for col in ("thermal_y", "downdraft_y"):
        if col not in df.columns:
            df[col] = 0.0
    fx = df.get("weather_force_x", pd.Series(0.0, index=df.index))
    fz = df.get("weather_force_z", pd.Series(0.0, index=df.index))
    df["weather_force_h"] = (fx ** 2 + fz ** 2) ** 0.5

    fname = os.path.basename(path)

    if "drone_id" in df.columns:
        results = []
        for idx, (did, group) in enumerate(df.groupby("drone_id", sort=False)):
            is_col = group.get("event", pd.Series(dtype=str)).fillna("") == "collision"
            results.append((group[~is_col].copy(), group[is_col].copy(), f"#{idx + 1}"))
        return results
    else:
        is_col = df.get("event", pd.Series(dtype=str)).fillna("") == "collision"
        return [(df[~is_col].copy(), df[is_col].copy(), fname)]


# ── File picker dialog ────────────────────────────────────────────────────────

class FilePickerDialog(tk.Toplevel):
    def __init__(self, parent, folder, t, s):
        super().__init__(parent)
        self._folder   = folder
        self._t        = t
        self._s        = s
        self._selected = []

        self.title(s["pick_title"])
        self.configure(bg=t["bg"])
        self.minsize(540, 320)
        self.resizable(True, True)
        self.withdraw()
        self.transient(parent)
        self.grab_set()

        self._build()
        self._load_files()

        w, h = 720, 480
        mx = parent.winfo_x() + (parent.winfo_width()  - w) // 2
        my = parent.winfo_y() + (parent.winfo_height() - h) // 2
        self.geometry(f"{w}x{h}+{mx}+{my}")
        self.deiconify()

    def _build(self):
        t = self._t; s = self._s

        # Folder path label + open in Finder button
        top = tk.Frame(self, bg=t["bg"], padx=14, pady=8)
        top.pack(fill=tk.X)
        tk.Button(top, text=s["open_finder"],
                  command=lambda: __import__("subprocess").run(["open", self._folder]),
                  bg=t["accent"], fg=t["btn_fg"], relief=tk.FLAT,
                  padx=8, pady=2, font=("Helvetica", 10, "bold"),
                  cursor="hand2").pack(side=tk.RIGHT)
        tk.Label(top, text=self._folder, bg=t["bg"], fg=t["fg2"],
                 font=("Helvetica", 10), anchor=tk.W).pack(side=tk.LEFT, fill=tk.X, expand=True)

        # Separator line
        tk.Frame(self, bg=t["bg3"], height=1).pack(fill=tk.X, padx=14)

        # Bottom buttons (packed before tree_frame so they are always visible)
        bf = tk.Frame(self, bg=t["bg"], padx=14, pady=12)
        bf.pack(side=tk.BOTTOM, fill=tk.X)

        self._open_btn = tk.Button(
            bf, text=s["open_sel"], command=self._confirm,
            bg=t["accent"], fg=t["btn_fg"], relief=tk.FLAT,
            padx=18, pady=6, font=("Helvetica", 12, "bold"),
            cursor="hand2", state=tk.DISABLED)
        self._open_btn.pack(side=tk.RIGHT, padx=(6, 0))

        tk.Button(bf, text=s["cancel"], command=self.destroy,
                  bg=t["accent"], fg=t["btn_fg"], activebackground=t["accent"],
                  relief=tk.FLAT, padx=18, pady=6,
                  font=("Helvetica", 12, "bold"),
                  cursor="hand2").pack(side=tk.RIGHT)

        # Hint label (above buttons)
        self._hint = tk.Label(self, text="", bg=t["bg"], fg=t["fg2"],
                               font=("Helvetica", 10))
        self._hint.pack(side=tk.BOTTOM, pady=(4, 0))

        # Treeview (fills remaining space)
        tree_frame = tk.Frame(self, bg=t["bg"])
        tree_frame.pack(fill=tk.BOTH, expand=True, padx=14, pady=(8, 0))

        style = ttk.Style()
        style.configure("FP.Treeview",
                         background=t["bg2"], foreground=t["fg"],
                         fieldbackground=t["bg2"],
                         rowheight=44, font=("Helvetica", 11))
        style.configure("FP.Treeview.Heading",
                         background=t["bg3"], foreground=t["fg"],
                         font=("Helvetica", 11, "bold"), relief="flat",
                         padding=[6, 10, 6, 10])
        style.layout("FP.Treeview.Heading", [
            ("Treeview.heading", {"sticky": "nswe", "children": [
                ("Treeview.padding", {"sticky": "nswe", "children": [
                    ("Treeview.label", {"sticky": "we"})
                ]})
            ]})
        ])
        style.map("FP.Treeview",
                  background=[("selected", t["sel_bg"])],
                  foreground=[("selected", t["sel_fg"])])

        self._tree = ttk.Treeview(tree_frame,
                                   columns=("name", "size", "modified"),
                                   show="headings",
                                   selectmode="extended",
                                   style="FP.Treeview")
        self._tree.heading("name",     text=s["col_name"],     anchor=tk.W,
                            command=lambda: self._sort("name"))
        self._tree.heading("size",     text=s["col_size"],     anchor=tk.W,
                            command=lambda: self._sort("size"))
        self._tree.heading("modified", text=s["col_modified"], anchor=tk.W,
                            command=lambda: self._sort("modified"))
        self._tree.column("name",     width=340, minwidth=180, anchor=tk.W)
        self._tree.column("size",     width=90,  minwidth=60,  anchor=tk.E)
        self._tree.column("modified", width=200, minwidth=140, anchor=tk.W)

        odd_bg  = t["bg2"]
        even_bg = t["bg3"]
        self._tree.tag_configure("odd",  background=odd_bg,  foreground=t["fg"])
        self._tree.tag_configure("even", background=even_bg, foreground=t["fg"])

        sb = ttk.Scrollbar(tree_frame, orient=tk.VERTICAL,
                            command=self._tree.yview)
        self._tree.configure(yscrollcommand=sb.set)
        sb.pack(side=tk.RIGHT, fill=tk.Y)
        self._tree.pack(fill=tk.BOTH, expand=True)

        self._tree_frame = tree_frame
        self._sep_color  = t["fg2"]

        self._tree.bind("<<TreeviewSelect>>", self._on_select)
        self._tree.bind("<Double-Button-1>",  lambda _: self._confirm())
        self._tree.bind("<Return>",            lambda _: self._confirm())
        self._tree.bind("<Button-1>",          self._click_anchor, add=True)
        self._tree.bind("<Control-Button-1>",  self._ctrl_click)
        self._tree.bind("<Up>",        lambda e: self.after_idle(self._update_anchor))
        self._tree.bind("<Down>",      lambda e: self.after_idle(self._update_anchor))
        self._tree.bind("<Shift-Up>",   lambda e: self._shift_move(-1))
        self._tree.bind("<Shift-Down>", lambda e: self._shift_move(1))
        self._anchor = None

        self._sort_col = "modified"
        self._sort_rev = True

    def _load_files(self):
        self._tree.delete(*self._tree.get_children())
        try:
            entries = []
            for fname in os.listdir(self._folder):
                if not fname.lower().endswith(".csv"):
                    continue
                path  = os.path.join(self._folder, fname)
                size  = os.path.getsize(path)
                mtime = os.path.getmtime(path)
                entries.append((fname, size, mtime))

            entries.sort(key=lambda x: x[2], reverse=True)

            for idx, (fname, size, mtime) in enumerate(entries):
                dt  = datetime.fromtimestamp(mtime).strftime("%Y-%m-%d  %H:%M:%S")
                tag = "odd" if idx % 2 == 0 else "even"
                self._tree.insert("", tk.END, iid=fname, tags=(tag,),
                                   values=(fname, f"{size/1024:.1f} KB", dt))

            count = len(entries)
            self._hint.configure(text=f"{count} file{'s' if count != 1 else ''}")
        except Exception as e:
            self._tree.insert("", tk.END, values=(str(e), "", ""))

        self.after(60, self._place_heading_sep)

    def _place_heading_sep(self):
        items = self._tree.get_children()
        if not items:
            return
        bbox = self._tree.bbox(items[0])
        if not bbox:
            self.after(60, self._place_heading_sep)
            return
        y = self._tree.winfo_y() + bbox[1] - 1
        sep = tk.Frame(self._tree_frame, height=1, bg=self._sep_color,
                       bd=0, highlightthickness=0)
        sep.place(x=0, y=y, relwidth=1.0)
        sep.tkraise()

    def _click_anchor(self, event):
        item = self._tree.identify_row(event.y)
        if item:
            self._anchor = item

    def _ctrl_click(self, event):
        item = self._tree.identify_row(event.y)
        if not item:
            return "break"
        sel = list(self._tree.selection())
        if item in sel:
            sel.remove(item)
        else:
            sel.append(item)
        self._tree.selection_set(sel)
        self._anchor = item
        return "break"

    def _update_anchor(self):
        focused = self._tree.focus()
        if focused:
            self._anchor = focused

    def _shift_move(self, direction):
        focused = self._tree.focus()
        items   = self._tree.get_children()
        if not items or not focused or focused not in items:
            return "break"
        if self._anchor is None or self._anchor not in items:
            self._anchor = focused
        idx      = list(items).index(focused)
        new_idx  = max(0, min(len(items) - 1, idx + direction))
        new      = items[new_idx]
        anc_idx  = list(items).index(self._anchor)
        lo, hi   = sorted([anc_idx, new_idx])
        self._tree.selection_set(items[lo:hi + 1])
        self._tree.focus(new)
        self._tree.see(new)
        return "break"

    def _sort(self, col):
        rows = [(self._tree.set(k, col), k) for k in self._tree.get_children()]
        rev  = (self._sort_col == col) and not self._sort_rev
        rows.sort(reverse=rev, key=lambda x: x[0].lower() if col == "name" else x[0])
        for i, (_, k) in enumerate(rows):
            self._tree.move(k, "", i)
            self._tree.item(k, tags=("odd" if i % 2 == 0 else "even",))
        self._sort_col = col
        self._sort_rev = rev

    def _on_select(self, _):
        n = len(self._tree.selection())
        state = tk.NORMAL if n > 0 else tk.DISABLED
        self._open_btn.configure(state=state)
        total = len(self._tree.get_children())
        if n > 1:
            self._hint.configure(text=f"{n} files selected")
        else:
            self._hint.configure(text=f"{total} file{'s' if total != 1 else ''}")

    def _confirm(self):
        sel = self._tree.selection()
        if not sel:
            return
        self._selected = [os.path.join(self._folder, iid) for iid in sel]
        self.destroy()

    @property
    def selected(self):
        return self._selected


# ── Themed matplotlib toolbar ─────────────────────────────────────────────────

class _ToolbarMixin:
    def __init__(self, canvas, parent, theme, app):
        self._t   = theme
        self._app = app
        super().__init__(canvas, parent, pack_toolbar=False)

    def configure_subplots(self, *args):
        import matplotlib as mpl
        from matplotlib import widgets
        from matplotlib.figure import Figure

        # Already open: re-center and bring to front
        if hasattr(self, "subplot_tool"):
            win = self.subplot_tool.figure.canvas.manager.window
            self._center_win(win)
            win.deiconify()
            win.lift()
            return

        with mpl.rc_context({"toolbar": "none"}):
            manager = type(self.canvas).new_manager(Figure(figsize=(6, 3)), -1)
        manager.set_window_title("Subplot configuration tool")
        tool_fig = manager.canvas.figure
        tool_fig.subplots_adjust(top=0.9)
        self.subplot_tool = widgets.SubplotTool(self.canvas.figure, tool_fig)
        cid = self.canvas.mpl_connect("close_event", lambda e: manager.destroy())

        def on_tool_fig_close(e):
            self.canvas.mpl_disconnect(cid)
            del self.subplot_tool

        tool_fig.canvas.mpl_connect("close_event", on_tool_fig_close)

        # Reset button: accent color
        try:
            t   = self._t
            btn = self.subplot_tool.buttonreset
            btn.color      = t["accent"]
            btn.hovercolor = t["sel_bg"]
            btn._set_button_color(t["accent"])
            tool_fig.canvas.draw_idle()
        except Exception:
            pass

        # Show centered without flash
        win = manager.window
        win.withdraw()
        manager._shown = True  # prevent manager.show() from calling deiconify
        tool_fig.canvas.draw()
        self._center_win(win)
        win.deiconify()
        win.lift()
        return self.subplot_tool

    def _center_win(self, win):
        win.update_idletasks()
        w = win.winfo_reqwidth() or 600
        h = win.winfo_reqheight() or 300
        mx = self._app.winfo_x() + (self._app.winfo_width()  - w) // 2
        my = self._app.winfo_y() + (self._app.winfo_height() - h) // 2
        win.geometry(f"{w}x{h}+{mx}+{my}")



# ── Main App ──────────────────────────────────────────────────────────────────

class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title(APP_TITLE)
        self.geometry("1150x780")
        self.minsize(800, 600)

        cfg = load_config()
        self._logs        = []
        self._theme       = cfg.get("theme", "dark")
        self._lang        = cfg.get("lang", "en")
        self._log_folder    = cfg.get("log_folder", "")
        self._export_folder = cfg.get("export_folder", "")
        self._main_frame    = None

        _setup_icon_guard()
        self._apply_mpl()
        self._rebuild_ui()
        self.protocol("WM_DELETE_WINDOW", self._on_close)

    def _on_close(self):
        self.quit()
        self.destroy()

    @property
    def _t(self): return THEMES[self._theme]
    @property
    def _s(self): return STRINGS[self._lang]

    # ── Matplotlib style ──────────────────────────────────────────────────────
    def _apply_mpl(self):
        if not _mpl_loaded:
            return
        t = self._t
        plt.style.use("dark_background" if self._theme == "dark" else "default")
        # Resolve a Japanese-capable font available on this system
        import matplotlib.font_manager as fm
        jp_font = next(
            (f.name for f in fm.fontManager.ttflist
             if any(k in f.name for k in ("Hiragino", "IPAex", "Noto Sans CJK", "Yu Gothic", "Meiryo"))),
            None
        )
        font_family = [jp_font, "sans-serif"] if jp_font else ["sans-serif"]
        matplotlib.rcParams.update({
            "figure.facecolor": t["bg"],
            "axes.facecolor":   t["bg2"],
            "axes.edgecolor":   "#444" if self._theme == "dark" else "#aaa",
            "grid.color":       "#333" if self._theme == "dark" else "#ccc",
            "text.color":       t["fg"],
            "axes.labelcolor":  t["fg"],
            "xtick.color":      t["fg2"],
            "ytick.color":      t["fg2"],
            "font.family":      font_family,
        })
        if getattr(self, "_export_folder", ""):
            matplotlib.rcParams["savefig.directory"] = self._export_folder

    # ── Full UI rebuild ───────────────────────────────────────────────────────
    def _rebuild_ui(self):
        if self._main_frame:
            self._main_frame.destroy()
        self.configure(bg=self._t["bg"])
        self._main_frame = tk.Frame(self, bg=self._t["bg"])
        self._main_frame.pack(fill=tk.BOTH, expand=True)
        self._build_toolbar()
        self._build_notebook()
        if self._logs:
            self._refresh()

    # ── Toolbar ───────────────────────────────────────────────────────────────
    def _build_toolbar(self):
        t = self._t; s = self._s
        bar = tk.Frame(self._main_frame, bg=t["bg"], pady=8, padx=12)
        bar.pack(side=tk.TOP, fill=tk.X)

        self._make_btn(bar, s["open"],  self._open_files).pack(side=tk.LEFT, padx=(0, 6))
        self._make_btn(bar, s["clear"], self._clear      ).pack(side=tk.LEFT)

        self._make_btn(bar, s["settings"], self._open_settings).pack(side=tk.RIGHT, padx=(6, 0))
        self._make_btn(bar, s["help"],     self._open_help    ).pack(side=tk.RIGHT)

    def _make_btn(self, parent, text, command):
        t = self._t
        return tk.Button(parent, text=text, command=command,
                         bg=t["accent"], fg=t["btn_fg"],
                         activebackground=t["accent"],
                         relief=tk.FLAT, padx=16, pady=6, cursor="hand2",
                         font=("Helvetica", 12, "bold"))

    # ── Help dialog ───────────────────────────────────────────────────────────
    def _open_help(self):
        t = self._t; s = self._s
        dlg = tk.Toplevel(self)
        dlg.title(s["help_title"])
        dlg.configure(bg=t["bg"])
        w, h = 480, 560
        mx = self.winfo_x() + (self.winfo_width()  - w) // 2
        my = self.winfo_y() + (self.winfo_height() - h) // 2
        dlg.geometry(f"{w}x{h}+{mx}+{my}")
        dlg.resizable(False, True)
        dlg.transient(self)
        dlg.grab_set()

        text = tk.Text(dlg, bg=t["bg2"], fg=t["fg"],
                       font=("Courier", 12), relief=tk.FLAT,
                       padx=16, pady=14, state=tk.NORMAL,
                       wrap=tk.WORD)
        sb = tk.Scrollbar(dlg, command=text.yview)
        text.configure(yscrollcommand=sb.set)
        sb.pack(side=tk.RIGHT, fill=tk.Y)
        text.pack(fill=tk.BOTH, expand=True)
        text.insert("1.0", s["help_body"])
        text.configure(state=tk.DISABLED)

        bf = tk.Frame(dlg, bg=t["bg"], pady=10)
        bf.pack(fill=tk.X, padx=14)
        self._make_btn(bf, s["close"], dlg.destroy).pack(side=tk.RIGHT)

    # ── Settings dialog ───────────────────────────────────────────────────────
    def _open_settings(self):
        t = self._t; s = self._s
        dlg = tk.Toplevel(self)
        dlg.title(s["settings"])
        dlg.configure(bg=t["bg"])
        dlg.resizable(False, False)
        dlg.withdraw()
        dlg.transient(self)
        dlg.grab_set()

        pad = {"padx": 24, "pady": 10}

        # Language
        tk.Label(dlg, text=s["language"], bg=t["bg"], fg=t["fg"],
                 font=("Helvetica", 13)).grid(row=0, column=0, sticky="w", **pad)
        lang_var = tk.StringVar(value=self._lang)
        lf = tk.Frame(dlg, bg=t["bg"])
        lf.grid(row=0, column=1, sticky="w", **pad)
        for val, lbl in (("en", "English"), ("ja", "日本語")):
            tk.Radiobutton(lf, text=lbl, variable=lang_var, value=val,
                           bg=t["bg"], fg=t["fg"], selectcolor=t["bg2"],
                           activebackground=t["bg"],
                           font=("Helvetica", 12)).pack(side=tk.LEFT, padx=8)

        # Dark mode
        tk.Label(dlg, text=s["dark_mode"], bg=t["bg"], fg=t["fg"],
                 font=("Helvetica", 13)).grid(row=1, column=0, sticky="w", **pad)
        dark_var = tk.BooleanVar(value=(self._theme == "dark"))
        tk.Checkbutton(dlg, variable=dark_var, bg=t["bg"], fg=t["fg"],
                       selectcolor=t["bg2"], activebackground=t["bg"]
                       ).grid(row=1, column=1, sticky="w", **pad)

        def _make_folder_row(row, label_key, var):
            tk.Label(dlg, text=s[label_key], bg=t["bg"], fg=t["fg"],
                     font=("Helvetica", 13)).grid(row=row, column=0, sticky="w", **pad)
            ff = tk.Frame(dlg, bg=t["bg"])
            ff.grid(row=row, column=1, sticky="ew", **pad)
            tk.Entry(ff, textvariable=var, bg=t["bg2"], fg=t["fg"],
                     insertbackground=t["fg"],
                     relief=tk.FLAT, font=("Helvetica", 11), width=32).pack(side=tk.LEFT, ipady=4, padx=(0, 6))
            tk.Button(ff, text=s["browse"],
                      command=lambda v=var: v.set(
                          filedialog.askdirectory(initialdir=v.get() or os.getcwd()) or v.get()),
                      bg=t["accent"], fg=t["btn_fg"], relief=tk.FLAT,
                      padx=10, pady=3, font=("Helvetica", 11, "bold"),
                      cursor="hand2").pack(side=tk.LEFT)

        folder_var  = tk.StringVar(value=self._log_folder)
        export_var  = tk.StringVar(value=self._export_folder)
        _make_folder_row(2, "log_folder",    folder_var)
        _make_folder_row(3, "export_folder", export_var)

        # Buttons
        bf = tk.Frame(dlg, bg=t["bg"])
        bf.grid(row=4, column=0, columnspan=2, pady=(4, 20))

        def apply():
            self._lang          = lang_var.get()
            self._theme         = "dark" if dark_var.get() else "light"
            self._log_folder    = folder_var.get().strip()
            self._export_folder = export_var.get().strip()
            save_config({"lang": self._lang, "theme": self._theme,
                         "log_folder": self._log_folder,
                         "export_folder": self._export_folder})
            if self._export_folder:
                import matplotlib
                matplotlib.rcParams["savefig.directory"] = self._export_folder
            self._apply_mpl()
            self._rebuild_ui()
            dlg.destroy()

        tk.Button(bf, text=s["apply"], command=apply,
                  bg=t["accent"], fg=t["btn_fg"], relief=tk.FLAT,
                  padx=16, pady=5, font=("Helvetica", 12, "bold"),
                  cursor="hand2").pack(side=tk.LEFT, padx=6)
        tk.Button(bf, text=s["cancel"], command=dlg.destroy,
                  bg=t["accent"], fg=t["btn_fg"], relief=tk.FLAT,
                  padx=16, pady=5, font=("Helvetica", 12, "bold"),
                  cursor="hand2").pack(side=tk.LEFT, padx=6)

        dlg.update_idletasks()
        w  = dlg.winfo_reqwidth();  h  = dlg.winfo_reqheight()
        mx = self.winfo_x() + (self.winfo_width()  - w) // 2
        my = self.winfo_y() + (self.winfo_height() - h) // 2
        dlg.geometry(f"{w}x{h}+{mx}+{my}")
        dlg.deiconify()

    # ── Custom tab system ─────────────────────────────────────────────────────
    def _build_notebook(self):
        t = self._t; s = self._s

        container = tk.Frame(self._main_frame, bg=t["bg"])
        container.pack(fill=tk.BOTH, expand=True, padx=8, pady=(0, 8))

        # Tab button bar
        tab_bar = tk.Frame(container, bg=t["bg3"])
        tab_bar.pack(side=tk.TOP, fill=tk.X)

        # Content area border (black in light mode, white in dark mode)
        border_color = "#000000" if self._theme == "light" else "#ffffff"
        border = tk.Frame(container, bg=border_color, padx=1, pady=1)
        border.pack(fill=tk.BOTH, expand=True)
        self._tab_content = tk.Frame(border, bg=t["bg"])
        self._tab_content.pack(fill=tk.BOTH, expand=True)

        self._frame_ts   = tk.Frame(self._tab_content, bg=t["bg"], bd=0, highlightthickness=0)
        self._frame_3d   = tk.Frame(self._tab_content, bg=t["bg"], bd=0, highlightthickness=0)
        self._frame_info = tk.Frame(self._tab_content, bg=t["bg"], bd=0, highlightthickness=0)
        self._tab_frames = [self._frame_ts, self._frame_3d, self._frame_info]

        self._tab_btns   = []
        self._tab_accents = []
        for i, label in enumerate([s["tab_ts"], s["tab_3d"], s["tab_info"]]):
            wrap = tk.Frame(tab_bar, bg=t["tab_unsel"], bd=0, highlightthickness=0)
            wrap.pack(side=tk.LEFT, padx=(0, 2))

            accent = tk.Frame(wrap, height=3, bd=0, highlightthickness=0)
            accent.pack(fill=tk.X)

            btn = tk.Button(wrap, text=label.strip(),
                            command=lambda idx=i: self._select_tab(idx),
                            relief=tk.FLAT, cursor="hand2", bd=0,
                            highlightthickness=0,
                            font=("Helvetica", 12), padx=16, pady=8)
            btn.pack()
            self._tab_btns.append(btn)
            self._tab_accents.append(accent)

        # Stack all frames in the same space; switching just raises the active one
        for frame in self._tab_frames:
            frame.place(relx=0, rely=0, relwidth=1, relheight=1)

        self._select_tab(0)
        self._build_info_tab()

    def _select_tab(self, idx):
        t = self._t
        for i, (btn, accent, frame) in enumerate(
                zip(self._tab_btns, self._tab_accents, self._tab_frames)):
            if i == idx:
                btn.configure(bg=t["tab_sel"], fg=t["accent"])
                accent.configure(bg=t["accent"])
                btn.master.configure(bg=t["tab_sel"])
                frame.tkraise()
            else:
                btn.configure(bg=t["tab_unsel"], fg=t["tab_unsel_fg"])
                accent.configure(bg=t["tab_unsel"])
                btn.master.configure(bg=t["tab_unsel"])

    def _build_info_tab(self):
        t = self._t
        sb = tk.Scrollbar(self._frame_info)
        sb.pack(side=tk.RIGHT, fill=tk.Y)
        self._info_text = tk.Text(
            self._frame_info, bg=t["bg"], fg=t["fg"],
            font=("Courier", 12), relief=tk.FLAT,
            bd=0, highlightthickness=0,
            padx=16, pady=12, state=tk.DISABLED,
            yscrollcommand=sb.set)
        self._info_text.pack(fill=tk.BOTH, expand=True)
        sb.config(command=self._info_text.yview)

    # ── File operations ───────────────────────────────────────────────────────
    def _open_files(self):
        s = self._s
        folder = self._log_folder

        if not folder or not os.path.isdir(folder):
            messagebox.showinfo(s["settings"], s["no_folder"])
            return

        csvs = [f for f in os.listdir(folder) if f.lower().endswith(".csv")]
        if not csvs:
            messagebox.showinfo(s["settings"], s["folder_empty"])
            return

        if not _mpl_loaded:
            self.title(f"{APP_TITLE} — Initializing...")
            self.update()
            _ensure_matplotlib()
            self.title(APP_TITLE)

        dlg = FilePickerDialog(self, folder, self._t, s)
        self.wait_window(dlg)
        self.update()
        self._restore_dock_icon()

        paths = list(dlg.selected)
        if not paths:
            return

        errors = []
        for path in paths:
            try:
                self._logs.extend(load_csv(path))
            except Exception as e:
                errors.append(str(e))

        for e in errors:
            messagebox.showerror(s["load_err"], e)

        if self._logs:
            self._refresh()

    def _clear(self):
        self._logs.clear()
        for frame in (self._frame_ts, self._frame_3d):
            for w in frame.winfo_children():
                w.destroy()
        self._set_info("")

    # ── Refresh all tabs ──────────────────────────────────────────────────────
    def _refresh(self):
        _ensure_matplotlib()
        self._apply_mpl()
        self._draw_timeseries()
        self._draw_3d()
        self._draw_info()
        self._restore_dock_icon()

    # ── Time Series ───────────────────────────────────────────────────────────
    def _draw_timeseries(self):
        s = self._s
        for w in self._frame_ts.winfo_children():
            w.destroy()

        has_building = any(
            df["thermal_y"].abs().sum() > 0 or df["downdraft_y"].abs().sum() > 0
            for df, _, _ in self._logs)

        n = 3 if has_building else 2
        fig, axes = plt.subplots(n, 1, figsize=(10, 3.0 * n), sharex=True)
        ax_list = list(axes)
        ax_alt, ax_spd = ax_list[0], ax_list[1]
        ax_bld = ax_list[2] if has_building else None
        zero_c = "#555" if self._theme == "dark" else "#999"

        bld_thermal_handles   = []
        bld_downdraft_handles = []
        bld_labels            = []

        for i, (df, coll, label) in enumerate(self._logs):
            c = COLORS[i % len(COLORS)]
            t = df["time"]
            ax_alt.plot(t, df["altitude"], color=c, lw=1.2, label=label)
            ax_spd.plot(t, df["speed"],    color=c, lw=1.2, label=label)
            if ax_bld is not None:
                ht, = ax_bld.plot(t, df["thermal_y"],   color=c, lw=1.0)
                hd, = ax_bld.plot(t, df["downdraft_y"], color=c, lw=1.0, ls="--")
                bld_thermal_handles.append(ht)
                bld_downdraft_handles.append(hd)
                bld_labels.append(label)
            for row in coll.itertuples():
                for ax in ax_list:
                    ax.axvline(x=row.time, color="#ff4444", lw=0.9, alpha=0.7, ls=":")

        fg = self._t["fg"]
        ax_alt.set_ylabel(s["alt_m"], rotation=0, ha="center", va="center", labelpad=40, multialignment="center")
        ax_alt.text(0.01, 0.97, s["alt"], transform=ax_alt.transAxes,
                    va="top", ha="left", fontsize=10, fontweight="bold", color=fg)
        ax_spd.set_ylabel(s["spd_ms"], rotation=0, ha="center", va="center", labelpad=40, multialignment="center")
        ax_spd.text(0.01, 0.97, s["spd"], transform=ax_spd.transAxes,
                    va="top", ha="left", fontsize=10, fontweight="bold", color=fg)

        if ax_bld:
            ax_bld.set_ylabel(s["force_n"], rotation=0, ha="center", va="center", labelpad=40, multialignment="center")
            ax_bld.text(0.01, 0.97, s["bld_effect"], transform=ax_bld.transAxes,
                        va="top", ha="left", fontsize=10, fontweight="bold", color=fg)
            ax_bld.set_xlabel(s["time_s"])
            ax_bld.axhline(0, color=zero_c, lw=0.8)
            ax_bld.grid(True, alpha=0.3)
        else:
            ax_spd.set_xlabel(s["time_s"])

        n_logs = len(self._logs)
        legend_kw = dict(fontsize=7, ncol=n_logs, loc="lower left",
                         bbox_to_anchor=(0, 1.0), frameon=False, borderaxespad=0)
        for ax in (ax_alt, ax_spd):
            ax.grid(True, alpha=0.3)
            ax.legend(**legend_kw)

        if ax_bld and bld_labels:
            # Interleave thermal/downdraft in column-major order:
            #   Row 0: #1 #2 ... #n  (thermal, solid)
            #   Row 1: #1 #2 ... #n  (downdraft, dashed)
            # ncol=n_logs matches alt/spd → #1 column positions align exactly
            handles_bld = []
            labels_bld  = []
            for th, dr, lbl in zip(bld_thermal_handles, bld_downdraft_handles, bld_labels):
                handles_bld.extend([th, dr])
                labels_bld.extend([lbl, lbl])
            leg_bld = ax_bld.legend(
                handles=handles_bld,
                labels=labels_bld,
                fontsize=7,
                ncol=n_logs,
                loc="lower left",
                bbox_to_anchor=(0, 1.0),
                frameon=False,
                borderaxespad=0,
            )
            leg_bld.set_title(
                s["thermal"] + ": ─   " + s["downdraft"] + ": - -",
                prop={"size": 6}
            )

        fig.tight_layout(pad=1.2, h_pad=5.0)

        self._embed(fig, self._frame_ts, "time_series")

    # ── 3D Path ───────────────────────────────────────────────────────────────
    def _draw_3d(self):
        t = self._t; s = self._s
        for w in self._frame_3d.winfo_children():
            w.destroy()

        fig = plt.figure(figsize=(14, 6))
        ax  = fig.add_subplot(111, projection="3d")
        ax.set_facecolor(t["bg2"])

        for i, (df, coll, label) in enumerate(self._logs):
            c = COLORS[i % len(COLORS)]
            ax.plot(df["pos_x"], df["pos_z"], df["pos_y"],
                    color=c, lw=1.0, alpha=0.85, label=label)
            if len(df):
                sv = df.iloc[0]; ev = df.iloc[-1]
                ax.scatter(sv.pos_x, sv.pos_z, sv.pos_y,
                           color=c, s=50, marker="o", zorder=5)
                ax.scatter(ev.pos_x, ev.pos_z, ev.pos_y,
                           color=c, s=70, marker="x", zorder=5)
            if len(coll):
                ax.scatter(coll["pos_x"], coll["pos_z"], coll["pos_y"],
                           color="#ff4444", s=90, marker="^", zorder=6,
                           label=f"{label} {s['collision']}")

        # Widen gray pane faces by adding 50% margin to horizontal axes
        import numpy as np
        all_x   = np.concatenate([df["pos_x"].values for df, _, _ in self._logs])
        all_y   = np.concatenate([df["pos_z"].values for df, _, _ in self._logs])
        x_min, x_max = all_x.min(), all_x.max()
        y_min, y_max = all_y.min(), all_y.max()
        x_pad = (x_max - x_min) * 0.5
        y_pad = (y_max - y_min) * 0.5
        ax.set_xlim(x_min - x_pad, x_max + x_pad)
        ax.set_ylim(y_min - y_pad, y_max + y_pad)

        ax.view_init(elev=25, azim=-80)
        ax.xaxis.set_rotate_label(False)
        ax.yaxis.set_rotate_label(False)
        ax.set_xlabel(s["x_m"], labelpad=8)
        ax.set_ylabel(s["z_m"], labelpad=8)
        ax.set_zlabel("")
        ax.text2D(0.90, 0.86, s["alt_m_ax"], transform=ax.transAxes,
                  ha="center", va="bottom", fontsize=10, color=t["fg"])
        ax.set_title(s["3d_title"], loc="left", pad=4)
        ncol = max(1, (len(self._logs) + 7) // 8)
        ax.legend(fontsize=7, ncol=ncol,
                  loc="upper left", bbox_to_anchor=(1.05, 1),
                  borderaxespad=0)
        fig.tight_layout()
        fig.subplots_adjust(right=0.82)
        self._embed(fig, self._frame_3d, "3d_path")

    # ── Info tab ──────────────────────────────────────────────────────────────
    def _draw_info(self):
        s = self._s
        lines = []
        for df, coll, label in self._logs:
            dur = (df["time"].max() - df["time"].min()) if len(df) else 0
            lines += [
                "━" * 48,
                f"  {label}",
                "━" * 48,
                f"  {s['duration']}: {dur:.1f} s",
                f"  {s['samples']}: {len(df)}",
                f"  {s['collisions']}: {len(coll)}",
            ]
            if len(df):
                lines += [
                    f"  {s['max_alt']}: {df['altitude'].max():.2f} m",
                    f"  {s['max_spd']}: {df['speed'].max():.2f} m/s",
                ]
                if "weather_force_y" in df.columns:
                    lines.append(
                        f"  {s['weather_fy']}: "
                        f"{df['weather_force_y'].min():.4f} ~ "
                        f"{df['weather_force_y'].max():.4f}")
                if df["thermal_y"].abs().sum() > 0:
                    lines.append(f"  {s['thermal_y']}: max {df['thermal_y'].max():.4f}")
                if df["downdraft_y"].abs().sum() > 0:
                    lines.append(f"  {s['downdraft_y']}: min {df['downdraft_y'].min():.4f}")
                active = df["active_layers"].dropna().unique()
                if len(active):
                    lines.append(
                        f"  {s['active_layers']}: "
                        f"{' | '.join(str(a) for a in active[:6])}")
            lines.append("")
        self._set_info("\n".join(lines))

    def _set_info(self, text):
        self._info_text.configure(state=tk.NORMAL)
        self._info_text.delete("1.0", tk.END)
        self._info_text.insert("1.0", text)
        self._info_text.configure(state=tk.DISABLED)

    # ── Dock icon restore ─────────────────────────────────────────────────────
    def _restore_dock_icon(self):
        global _icon_change_allowed
        try:
            import sys, os, AppKit
            if getattr(sys, "frozen", False):
                resources = os.path.join(
                    os.path.dirname(os.path.dirname(sys.executable)),
                    "Resources")
            else:
                resources = os.path.dirname(os.path.abspath(__file__))
            path = os.path.join(resources, "icon.icns")
            if os.path.exists(path):
                img = AppKit.NSImage.alloc().initWithContentsOfFile_(path)
                if img:
                    _icon_change_allowed = True
                    AppKit.NSApp.setApplicationIconImage_(img)
                    _icon_change_allowed = False
        except Exception:
            _icon_change_allowed = False

    # ── Helper: embed matplotlib figure ──────────────────────────────────────
    def _image_filename(self, prefix):
        import re
        if self._logs:
            label = self._logs[0][2]  # e.g. "weather_20260507_123456"
            m = re.search(r"(\d{8}_\d{6})", label)
            if m:
                return f"{prefix}_{m.group(1)}.png"
        from datetime import datetime
        return f"{prefix}_{datetime.now().strftime('%Y%m%d_%H%M%S')}.png"

    def _embed(self, fig, parent, filename="image"):
        t = self._t
        canvas = FigureCanvasTkAgg(fig, master=parent)
        canvas.get_default_filename = lambda: self._image_filename(filename)
        canvas.draw()
        toolbar = _ThemedToolbar(canvas, parent, t, self)
        toolbar.update()
        toolbar.configure(bg=t["bg"])
        for child in toolbar.winfo_children():
            try:
                child.configure(bg=t["bg"], fg=t["fg"])
            except tk.TclError:
                pass
        toolbar.pack(side=tk.BOTTOM, fill=tk.X)
        w = canvas.get_tk_widget()
        w.configure(bg=t["bg"], highlightthickness=0)
        w.pack(fill=tk.BOTH, expand=True)


if __name__ == "__main__":
    app = App()
    app.mainloop()
