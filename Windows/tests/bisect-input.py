import json, subprocess, os

TMA_DIR = r"C:\Users\31576\LyricsX\Windows\tests\LyricsX.Windows.TestMediaApp"
OBJ = os.path.join(TMA_DIR, "obj", "Debug", "net10.0-windows10.0.22000.0")
XC = r"C:\Users\31576\.nuget\packages\microsoft.windowsappsdk.winui\1.8.260224000\tools\net472\XamlCompiler.exe"

tma = json.load(open(os.path.join(OBJ, "input.json"), encoding="utf-8"))
app = json.load(open(r"C:\Users\31576\LyricsX\Windows\src\LyricsX.Windows.App\obj\Debug\net10.0-windows10.0.22000.0\input.json", encoding="utf-8"))

def run(xaml_input, tag):
    inp = os.path.join(OBJ, f"b2-in-{tag}.json")
    out = os.path.join(OBJ, f"b2-out-{tag}.json")
    json.dump(xaml_input, open(inp, "w", encoding="utf-8"))
    p = subprocess.run([XC, inp, out], capture_output=True, cwd=TMA_DIR)
    return p.returncode

apps = app["XamlApplications"]
pages = app["XamlPages"]
print("app pages:", [p["FullPath"] for p in pages])

# application alone
t = json.loads(json.dumps(tma)); t["XamlApplications"] = apps
print("app XamlApplications alone:", run(t, "apps"))

# each page alone
for i, page in enumerate(pages):
    t = json.loads(json.dumps(tma)); t["XamlPages"] = [page]
    print(f"page[{i}] alone:", run(t, f"p{i}"), page["FullPath"])
