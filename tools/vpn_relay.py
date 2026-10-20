"""Forward Arena's three local ports through an authenticated Tailscale network.

Host mode binds only an IP reported for this Tailscale device. Client mode
binds only loopback and connects only to a 100.64.0.0/10 VPN address. Services
keep their existing localhost TLS names; private certificate keys stay hosted.
"""
import argparse
import asyncio
import ipaddress
import json
from pathlib import Path
import shutil
import subprocess

PORTS=(8787,9444,9445)
NETWORK=ipaddress.ip_network('100.64.0.0/10')


def tailscale():
    executable=shutil.which('tailscale')
    fallback=Path(r'C:\Program Files\Tailscale\tailscale.exe')
    if not executable and fallback.exists():executable=str(fallback)
    if not executable:
        raise RuntimeError('Install Tailscale and sign in first: https://tailscale.com/download/windows')
    result=subprocess.run([executable,'status','--json'],capture_output=True,text=True,check=True,
                          creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0),timeout=15)
    status=json.loads(result.stdout)
    if status.get('BackendState')!='Running':raise RuntimeError('Sign in to Tailscale first')
    return status


async def relay(reader,writer,target,port):
    upstream=None
    try:
        incoming,outgoing=await asyncio.wait_for(asyncio.open_connection(target,port),10)
        upstream=outgoing
        async def copy(source,destination):
            while chunk:=await source.read(65536):
                destination.write(chunk)
                await destination.drain()
        tasks=[asyncio.create_task(copy(reader,outgoing)),asyncio.create_task(copy(incoming,writer))]
        try:await asyncio.wait(tasks,return_when=asyncio.FIRST_COMPLETED)
        finally:
            for task in tasks:task.cancel()
            await asyncio.gather(*tasks,return_exceptions=True)
    except (OSError,TimeoutError,ConnectionError):
        pass
    finally:
        writer.close()
        if upstream:upstream.close()


async def main():
    parser=argparse.ArgumentParser(description=__doc__)
    group=parser.add_mutually_exclusive_group(required=True)
    group.add_argument('--host',action='store_true')
    group.add_argument('--check',action='store_true')
    group.add_argument('--connect',type=ipaddress.ip_address)
    args=parser.parse_args()
    status=tailscale()
    own=[ipaddress.ip_address(ip) for ip in status.get('Self',{}).get('TailscaleIPs',[]) if ':' not in ip]
    if args.check:
        if not own:raise RuntimeError('This device has no Tailscale IPv4 address')
        print('VPN ready:',own[0]);return
    if args.host:
        if not own:raise RuntimeError('This device has no Tailscale IPv4 address')
        bind=str(own[0]);target='127.0.0.1'
    else:
        if args.connect not in NETWORK:raise RuntimeError('Use the host Tailscale IPv4 address (100.x.x.x)')
        if args.connect in own:raise RuntimeError('Choose another device, not this computer')
        bind='127.0.0.1';target=str(args.connect)
    servers=[]
    try:
        for port in PORTS:
            _,probe=await asyncio.wait_for(asyncio.open_connection(target,port),8)
            probe.close();await probe.wait_closed()
        for port in PORTS:
            async def accept(reader,writer,port=port):await relay(reader,writer,target,port)
            servers.append(await asyncio.start_server(accept,bind,port,limit=65536))
        print(('Share this VPN server address: '+bind) if args.host else 'Connected. Open http://127.0.0.1:8787 and sign in to your own server account.',flush=True)
        print('Keep this window open. No router port forwarding is required.',flush=True)
        await asyncio.gather(*(server.serve_forever() for server in servers))
    finally:
        for server in servers:server.close()
        await asyncio.gather(*(server.wait_closed() for server in servers))


if __name__=='__main__':
    try:asyncio.run(main())
    except KeyboardInterrupt:pass
