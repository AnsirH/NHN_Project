"""Load the actual Unity artifact at a GitHub Pages-shaped project subpath."""
import argparse
import functools
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path
import threading

from playwright.sync_api import sync_playwright


class ProjectHandler(SimpleHTTPRequestHandler):
    def do_GET(self):
        if not self.path.startswith('/NHN_Project/'):
            self.send_error(404)
            return
        self.path = self.path[len('/NHN_Project'):]
        super().do_GET()

    def log_message(self, *_args):
        pass


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--artifact', default='Build/WebGL')
    parser.add_argument('--url')
    parser.add_argument('--output', default='Logs/browser')
    args = parser.parse_args()
    output = Path(args.output)
    output.mkdir(parents=True, exist_ok=True)
    server = None
    if args.url:
        url = args.url
    else:
        handler = functools.partial(ProjectHandler, directory=str(Path(args.artifact).resolve()))
        server = ThreadingHTTPServer(('127.0.0.1', 0), handler)
        threading.Thread(target=server.serve_forever, daemon=True).start()
        url = f'http://127.0.0.1:{server.server_port}/NHN_Project/'
    events = {'url': url, 'page_errors': [], 'http_errors': [], 'console_errors': []}
    try:
        with sync_playwright() as playwright:
            browser = playwright.chromium.launch(args=[
                '--enable-webgl', '--ignore-gpu-blocklist', '--use-angle=swiftshader',
                '--enable-unsafe-swiftshader',
            ])
            page = browser.new_page(viewport={'width': 1280, 'height': 900})
            page.on('pageerror', lambda error: events['page_errors'].append(str(error)))
            page.on('response', lambda response: events['http_errors'].append(
                f'{response.status} {response.url}') if response.status >= 400 else None)
            page.on('console', lambda message: events['console_errors'].append(message.text)
                    if message.type == 'error' else None)
            page.goto(url, wait_until='domcontentloaded', timeout=60000)
            page.wait_for_function("""() => {
                const bar = document.querySelector('#unity-loading-bar');
                const canvas = document.querySelector('#unity-canvas');
                return bar && bar.style.display === 'none' && canvas && canvas.width > 0;
            }""", timeout=240000)
            page.wait_for_timeout(5000)
            page.screenshot(path=str(output / 'main-menu.png'))
            info = page.request.get(url.rstrip('/') + '/build-info.json')
            if not info.ok:
                raise RuntimeError('Build metadata is unavailable.')
            events['build_info'] = info.json()
            if events['page_errors'] or events['http_errors'] or events['console_errors']:
                raise RuntimeError('WebGL runtime or HTTP errors occurred; see browser/results.json.')
            print('WebGL browser smoke passed:', json.dumps(events['build_info']))
            browser.close()
    finally:
        (output / 'results.json').write_text(json.dumps(events, indent=2), encoding='utf-8')
        if server:
            server.shutdown()


if __name__ == '__main__':
    main()
