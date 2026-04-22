using BCrypt.Net;

var hash = "$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewdBPj/RK.s5uoHi";
var password = "Demo1234!";
var result = BCrypt.Net.BCrypt.Verify(password, hash);
Console.WriteLine($"Hash matches 'Demo1234!': {result}");

// Generate a fresh hash
var newHash = BCrypt.Net.BCrypt.HashPassword(password, 12);
Console.WriteLine($"Fresh hash: {newHash}");
