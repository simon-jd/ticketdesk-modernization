# vm-counters.ps1 - Windows performance counters of the legacy VM, one CSV line per sample (Round 6 load).
# capacity.sh replaces __SAMPLES__ and __INTERVAL__ before it sends the script (vmctl.sh ps).
$c = '\Processor(_Total)\% Processor Time', '\Process(w3wp)\% Processor Time', '\Process(sqlservr)\% Processor Time',
     '\Process(w3wp)\Thread Count', '\System\Processor Queue Length', '\ASP.NET\Requests Queued',
     '\ASP.NET Applications(__Total__)\Requests Executing', '\HTTP Service Request Queues(ticketdesk)\CurrentQueueSize',
     '\MSSQL$SQLEXPRESS:SQL Statistics\Batch Requests/sec', '\MSSQL$SQLEXPRESS:Locks(_Total)\Lock Waits/sec',
     '\MSSQL$SQLEXPRESS:Locks(_Total)\Lock Wait Time (ms)', '\MSSQL$SQLEXPRESS:General Statistics\User Connections',
     '\MSSQL$SQLEXPRESS:Buffer Manager\Page life expectancy', '\PhysicalDisk(_Total)\Disk Transfers/sec'
$names = 'cpu_total_pct', 'w3wp_pct', 'sqlservr_pct', 'w3wp_threads', 'proc_queue', 'aspnet_queued', 'aspnet_executing',
         'http_queue', 'sql_batches_s', 'lock_waits_s', 'lock_wait_ms', 'sql_connections', 'page_life_s', 'disk_iops'
"t," + ($names -join ',')
Get-Counter -Counter $c -SampleInterval __INTERVAL__ -MaxSamples __SAMPLES__ -ErrorAction SilentlyContinue | ForEach-Object {
  $s = $_
  $v = foreach ($p in $c) {
    $m = $s.CounterSamples | Where-Object { $_.Path -like ('*' + $p.ToLower()) } | Select-Object -First 1
    if ($m) { [math]::Round($m.CookedValue, 1) } else { '' }
  }
  $s.Timestamp.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.fffZ') + ',' + ($v -join ',')
}
