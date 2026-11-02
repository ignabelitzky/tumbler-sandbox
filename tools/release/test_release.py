import json
from pathlib import Path
import stat
import tempfile
import unittest
import zipfile
from unittest.mock import patch
import release as r
import publish as pub

SHA = "a" * 40
TAG = "v0.1.0-alpha.1"


class ReleaseTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def fixture(self, platform="linux-x64", extra=None, executable=True):
        exe, player, magic = r.PLATFORMS[platform]
        path = self.root / f"tumbler-sandbox-{TAG}-{platform}.zip"
        files = {exe: magic + b"fixture", player: b"fixture",
                 "TumblerSandbox_Data/globalgamemanagers": b"fixture",
                 "TumblerSandbox_Data/boot.config": b"fixture",
                 "MonoBleedingEdge/EmbedRuntime/runtime": b"fixture",
                 "LICENSE": b"fixture", "NOTICE.md": b"fixture",
                 "receipt.json": json.dumps(dict(commit=SHA, unity=r.VERSION,
                                                platform=platform, development=False)).encode()}
        files.update(extra or {})
        with zipfile.ZipFile(path, "w") as z:
            for name, data in files.items():
                info = zipfile.ZipInfo(name)
                info.external_attr = (stat.S_IFREG | (0o755 if name == exe and executable else 0o644)) << 16
                z.writestr(info, data)
        manifest = dict(schema=1, tag=TAG, commit=SHA, unity=r.VERSION,
                        packages=[dict(file=path.name, platform=platform, sha256=r.digest(path))])
        (self.root / "manifest.json").write_text(json.dumps(manifest))
        return path

    def test_build_and_pack_round_trip(self):
        source = self.root / "tools/release"
        source.mkdir(parents=True)
        (source / "LocalReleaseBuild.cs").write_text("fixture helper")
        (self.root / "Assets").mkdir()
        (self.root / "docs").mkdir()
        (self.root / "docs/release-notice.md").write_text("notice")
        (self.root / "LICENSE").write_text("license")
        def compile_player(args, **kwargs):
            output = self.root / "Builds/Release/linux-x64"
            archive = self.fixture()
            with zipfile.ZipFile(archive) as z:
                z.extractall(output)
            (output / "receipt.json").unlink()
            (output / "LICENSE").unlink()
            (output / "NOTICE.md").unlink()
            (output / "TumblerSandbox.x86_64").chmod(0o755)
            (output / "build-complete.txt").write_text(r.VERSION)
            (output / "build.log").write_text("local diagnostics")
            self.assertNotIn("-quit", args)
            self.assertEqual(kwargs["env"]["TUMBLER_PLATFORM"], "linux-x64")
        with patch.object(r, "ROOT", self.root), patch.object(r, "clean", return_value=SHA):
            with patch.object(r.subprocess, "run", side_effect=compile_player):
                r.build("linux-x64", "/fake/Unity")
            self.assertFalse((self.root / "Assets/_LocalRelease").exists())
            with self.assertRaises(ValueError):
                r.build("linux-x64", "/fake/Unity")
            r.pack(TAG, ["linux-x64"])
            folder = self.root / "Builds/Packages" / TAG
            r.validate(folder, TAG, SHA)
            with zipfile.ZipFile(folder / f"tumbler-sandbox-{TAG}-linux-x64.zip") as z:
                self.assertNotIn("build.log", z.namelist())
            with self.assertRaises(FileExistsError):
                r.pack(TAG, ["linux-x64"])

    def test_build_failure_cleans_helper_and_has_no_receipt(self):
        import subprocess
        source = self.root / "tools/release"
        source.mkdir(parents=True)
        (source / "LocalReleaseBuild.cs").write_text("fixture")
        (self.root / "Assets").mkdir()
        with patch.object(r, "ROOT", self.root), patch.object(r, "clean", return_value=SHA), patch.object(r.subprocess, "run", side_effect=subprocess.CalledProcessError(1, "Unity")):
            with self.assertRaises(subprocess.CalledProcessError):
                r.build("linux-x64", "/fake/Unity")
        self.assertFalse((self.root / "Assets/_LocalRelease").exists())
        self.assertFalse((self.root / "Builds/Release/linux-x64/receipt.json").exists())

    def test_linux(self):
        self.fixture()
        self.assertEqual(len(r.validate(self.root, TAG, SHA)), 1)
        self.assertIn("linux-x64.zip", (self.root / "SHA256SUMS").read_text())

    def test_windows(self):
        self.fixture("windows-x64")
        r.validate(self.root, TAG, SHA)

    def test_wrong_commit(self):
        self.fixture()
        with self.assertRaises(ValueError):
            r.validate(self.root, TAG, "b" * 40)

    def test_receipt_commit(self):
        self.fixture(extra={"receipt.json": b"{}"})
        with self.assertRaises(ValueError):
            r.validate(self.root, TAG, SHA)

    def test_tampered_archive(self):
        path = self.fixture()
        with path.open("ab") as f:
            f.write(b"tamper")
        with self.assertRaises(ValueError):
            r.validate(self.root, TAG, SHA)

    def test_private_and_unsafe_paths(self):
        for name in ["../secret", "/absolute", "C:/file", "a\\b", "UnityEntitlementLicense.xml", "license.ulf", ".env"]:
            with self.subTest(name=name):
                self.fixture(extra={name: b"fixture"})
                with self.assertRaises(ValueError):
                    r.validate(self.root, TAG, SHA)

    def test_no_execute_permission(self):
        self.fixture(executable=False)
        with self.assertRaises(ValueError):
            r.validate(self.root, TAG, SHA)

    def test_missing_data(self):
        path = self.fixture()
        with zipfile.ZipFile(path, "w") as z:
            z.writestr("TumblerSandbox.x86_64", b"\x7fELF")
        with self.assertRaises(ValueError):
            r.check_zip(path, "linux-x64", SHA)

    def test_duplicate_platform(self):
        self.fixture()
        path = self.root / "manifest.json"
        m = json.loads(path.read_text())
        m["packages"] *= 2
        path.write_text(json.dumps(m))
        with self.assertRaises(ValueError):
            r.validate(self.root, TAG, SHA)

    def test_bad_tags(self):
        for tag in ["--help", "v1", "v1.2.3/other", "$(echo unsafe)", "v1.2.3\n"]:
            with self.subTest(tag=tag), self.assertRaises(ValueError):
                r.tag_ok(tag)

    def test_upload_then_publish(self):
        self.fixture()
        with patch.object(pub.subprocess, "check_output", return_value='{"assets":[],"isDraft":true}'), patch.object(pub.subprocess, "run") as call:
            pub.publish(self.root, TAG, SHA)
        commands = [c.args[0] for c in call.call_args_list]
        self.assertEqual(len(commands), 4)
        self.assertEqual(commands[-1][:3], ["gh", "release", "edit"])
        self.assertIn("--prerelease=true", commands[-1])

    def test_published_identical_assets_are_noop(self):
        self.fixture()
        files = r.validate(self.root, TAG, SHA) + ["manifest.json", "SHA256SUMS"]
        response = json.dumps(dict(assets=[dict(name=f) for f in files], isDraft=False))
        def download(args, **kwargs):
            import shutil
            shutil.copy(self.root / args[args.index("--pattern") + 1],
                        Path(args[args.index("--dir") + 1]))
        with patch.object(pub.subprocess, "check_output", return_value=response), patch.object(pub.subprocess, "run", side_effect=download) as call:
            pub.publish(self.root, TAG, SHA)
        self.assertTrue(all(c.args[0][2] == "download" for c in call.call_args_list))

    def test_different_existing_asset_aborts(self):
        self.fixture()
        response = '{"assets":[{"name":"manifest.json"}],"isDraft":true}'
        def download(args, **kwargs):
            (Path(args[-1]) / "manifest.json").write_text("different")
        with patch.object(pub.subprocess, "check_output", return_value=response), patch.object(pub.subprocess, "run", side_effect=download) as call:
            with self.assertRaises(ValueError):
                pub.publish(self.root, TAG, SHA)
        self.assertEqual(call.call_count, 1)


if __name__ == "__main__":
    unittest.main()
