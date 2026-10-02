"""Loopback-only HTTP fixture for CardArtDownloadValidation; no production hosting."""
import argparse
import http.server
import pathlib
import time

parser = argparse.ArgumentParser()
parser.add_argument('--image', required=True)
parser.add_argument('--port', type=int, default=18779)
args = parser.parse_args()
payload = pathlib.Path(args.image).read_bytes()

class Handler(http.server.BaseHTTPRequestHandler):
    retry_counts = {}

    def do_GET(self):
        if self.path.endswith('/404.jpg'):
            self.send_error(404)
            return
        if self.path.endswith('/retry.jpg'):
            count = Handler.retry_counts.get(self.path, 0) + 1
            Handler.retry_counts[self.path] = count
            if count == 1:
                self.send_error(503)
                return
        if self.path.endswith('/slow.jpg'):
            time.sleep(2)
        data = b'<html>not a card</html>' if self.path.endswith('/bad.jpg') else payload
        self.send_response(200)
        self.send_header('Content-Type', 'image/jpeg')
        self.send_header('Content-Length', str(len(data)))
        self.end_headers()
        try:
            self.wfile.write(data)
        except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError):
            pass

http.server.ThreadingHTTPServer(('127.0.0.1', args.port), Handler).serve_forever()
