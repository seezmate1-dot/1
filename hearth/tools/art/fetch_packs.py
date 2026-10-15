"""Downloads the free tiers of Quaternius' packs the art is made from (itch.io, "name your own price",
$0) and unpacks them into QUATERNIUS (default /home/user/quaternius). No account is needed.

  python hearth/tools/art/fetch_packs.py

Licence: CC0 / Quaternius Asset License - free in personal and commercial games, no credit
required; the models themselves may not be resold or redistributed as assets, so they are not kept
in this repository (only the pictures rendered from them are).
"""
import http.cookiejar
import json
import os
import re
import sys
import urllib.parse
import urllib.request
import zipfile

QUATERNIUS = os.environ.get("QUATERNIUS", "/home/user/quaternius")
PACKS = [
    "medieval-village-megakit", "fantasy-props-megakit", "stylized-nature-megakit", "universal-base-characters",
    "modular-character-outfits-fantasy", "universal-animation-library", "universal-animation-library-2",
    "bestiary-dungeon-monsters-kit",
]
CSRF = re.compile(r'csrf_token" (?:content|value)="([^"]+)"')


def fetch(slug, out):
    jar = http.cookiejar.CookieJar()
    web = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
    web.addheaders = [("User-Agent", "Mozilla/5.0"), ("X-Requested-With", "XMLHttpRequest")]

    def get(url, data=None):
        body = urllib.parse.urlencode(data).encode() if data is not None else None
        return web.open(url, body, timeout=120).read()

    page = "https://quaternius.itch.io/" + slug
    csrf = CSRF.search(get(page).decode()).group(1)
    download = json.loads(get(page + "/download_url", {"csrf_token": csrf}))["url"]
    html = get(download).decode()
    csrf = CSRF.search(html).group(1)
    uploads = zip(re.findall(r'data-upload_id="(\d+)"', html), re.findall(r'<strong title="([^"]+)" class="name"', html))
    for upload, name in uploads:
        url = json.loads(get("%s/file/%s?source=view_game&as_props=1" % (page, upload), {"csrf_token": csrf}))["url"]
        path = os.path.join(out, name)
        with web.open(url, timeout=600) as resp, open(path, "wb") as f:
            while True:
                chunk = resp.read(1 << 20)
                if not chunk:
                    break
                f.write(chunk)
        with zipfile.ZipFile(path) as z:
            z.extractall(out)
        os.remove(path)
        print("got", slug, name, flush=True)


if __name__ == "__main__":
    for slug in sys.argv[1:] or PACKS:
        out = os.path.join(QUATERNIUS, slug)
        if os.path.isdir(out) and os.listdir(out):
            print("have", slug)
            continue
        os.makedirs(out, exist_ok=True)
        fetch(slug, out)
