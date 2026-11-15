"""Upload an .aab to a Google Play track via the Play Developer API.

    python Tools/upload_play.py --key C:/project/play-service-account.json \
        --aab aab/ShipWar-0.9.0.aab [--track internal] [--package com.mataco.seewargame1] \
        [--status completed|draft] [--notes "What's new"]
    python Tools/upload_play.py --key ... --show [--track internal]   # print track, change nothing

Needs: pip install google-api-python-client google-auth
The service account must be invited in Play Console > Users and permissions
with release rights for the app. Use --status draft while the app has never
been published (Play rejects "completed" for draft apps).
"""
import argparse
import sys

from google.oauth2 import service_account
from googleapiclient.discovery import build
from googleapiclient.http import MediaFileUpload

SCOPES = ["https://www.googleapis.com/auth/androidpublisher"]


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--key", required=True)
    p.add_argument("--aab")
    p.add_argument("--package", default="com.mataco.seewargame1")
    p.add_argument("--track", default="internal")
    p.add_argument("--status", default="completed", choices=["completed", "draft"])
    p.add_argument("--notes", default="")
    p.add_argument("--show", action="store_true", help="print the track's releases and exit")
    a = p.parse_args()
    if not a.show and not a.aab:
        p.error("--aab is required unless --show")

    creds = service_account.Credentials.from_service_account_file(a.key, scopes=SCOPES)
    edits = build("androidpublisher", "v3", credentials=creds, cache_discovery=False).edits()

    edit_id = edits.insert(packageName=a.package, body={}).execute()["id"]
    if a.show:
        codes = []
        for t in edits.tracks().list(packageName=a.package, editId=edit_id).execute().get("tracks", []):
            for r in t.get("releases", []):
                codes += [int(c) for c in r.get("versionCodes", [])]
                if t["track"] == a.track:
                    print(f"{a.track}: {r.get('name')} codes={r.get('versionCodes')} status={r.get('status')}")
        print("max versionCode on any track:", max(codes) if codes else 0)
        edits.delete(packageName=a.package, editId=edit_id).execute()
        return 0
    print("edit", edit_id)

    media = MediaFileUpload(a.aab, mimetype="application/octet-stream", resumable=True)
    bundle = edits.bundles().upload(packageName=a.package, editId=edit_id, media_body=media).execute()
    code = bundle["versionCode"]
    print("uploaded versionCode", code)

    release = {"versionCodes": [str(code)], "status": a.status}
    if a.notes:
        release["releaseNotes"] = [{"language": "en-US", "text": a.notes}]
    edits.tracks().update(packageName=a.package, editId=edit_id, track=a.track,
                          body={"track": a.track, "releases": [release]}).execute()

    edits.commit(packageName=a.package, editId=edit_id).execute()
    print(f"committed: versionCode {code} -> {a.track} ({a.status})")


if __name__ == "__main__":
    sys.exit(main())
