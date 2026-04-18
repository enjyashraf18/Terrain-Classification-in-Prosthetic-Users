import socket

client = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
client.connect(("127.0.0.1", 5050))

print("Connected to server\n")

while True:
    data = client.recv(1024)
    if not data:
        break
    print(data.decode(), end="")