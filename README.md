# SourceQueryCacheMono
Query Cache ported to C# Mono and updated to handle all other queries.

Add this to your iptables for it to work:
```
iptables -t nat -I PREROUTING -p udp -d <Public IP> --dport <Server Port> -m u32 --u32  '0>>22&0x3C@8=0xFFFFFFFF && 0>>22&0x3C@12=0x54536F75 && 0>>22&0x3C@16=0x72636520 && 0>>22&0x3C@20=0x456E6769 && 0>>22&0x3C@24=0x6E652051 && 0>>22&0x3C@28=0x75657279' -j REDIRECT --to-port <Proxy Port>
```
Replace \<Public IP\>, \<Server Port\>, \<Proxy Port\>
E.g. If your server's IP is 1.2.3.4, your server was running on port 27015, and you ran QueryCache on 21015 then replace
Public IP = 1.2.3.4
Server Port = 27015
Proxy Port = 21015
