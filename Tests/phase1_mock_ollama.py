"""Test double only, not Ollama. Run outside the Unity Assets tree on loopback port 11435."""
import json, time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
MODEL = 'qwen2.5:7b'
class Handler(BaseHTTPRequestHandler):
    def reply(self, payload):
        data=json.dumps(payload).encode('utf-8')
        try:
            self.send_response(200); self.send_header('Content-Type','application/json')
            self.send_header('Content-Length',str(len(data))); self.end_headers(); self.wfile.write(data)
        except OSError: print('Client disconnected before response.',flush=True)
    def do_GET(self):
        if self.path!='/api/tags': self.send_error(404); return
        self.reply({'models':[{'name':MODEL}]})
    def do_POST(self):
        if self.path!='/api/generate': self.send_error(404); return
        n=int(self.headers.get('Content-Length','0')); payload=json.loads(self.rfile.read(n))
        request=payload.get('prompt','').split('\nPlayer request: ',1)[-1]
        print('POST',request[:100],flush=True)
        if 'hold_response' in request: time.sleep(30)
        if 'invalid_response' in request: response='not-json'
        else:
            response=json.dumps({'version':1,'message':'Deterministic test creation.',
                'objects':[{'id':'probe_cube','kind':'cube','color':'#FF0000','collider':'none'}], 'bindings':[]})
        self.reply({'response':response,'done':True})
if __name__=='__main__':
    print('Mock listening on 127.0.0.1:11435',flush=True)
    ThreadingHTTPServer(('127.0.0.1',11435),Handler).serve_forever()
