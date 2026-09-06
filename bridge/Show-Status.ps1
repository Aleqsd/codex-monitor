param([int]$Port = 43187)
$snapshot = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/api/threads"
$snapshot.threads | Select-Object @{Name='État';Expression={$_.label}}, @{Name='Tâche';Expression={$_.title}}, @{Name='Modèle';Expression={$_.model}} | Format-Table -AutoSize -Wrap
