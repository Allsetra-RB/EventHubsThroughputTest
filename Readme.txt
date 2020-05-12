In testing with Azure Event Hubs we noticed the following:
	* The service is very good at processing a lot of events at speed.
	* The service's processing speed fluctuates a lot at the reader side.
		* Writing to the Event Hub occurs at constant speed and in the graphs shows up as a horizontal, steady, line.
		* Reading from the Event Hub, even when the client code doesn't really do anything, fluctuates and shows up in
			the graphs as a 'square sine'-like graph that fluctuates around the write speed.
	* The vanilla disk lease manager can be agitated to underperform under certain circumstances.
		* When starting a reader at a steady 100 message batch and 10 millisecond processing speed, checkpointing looks ok.
		* If you leave it running a few minutes and upgrade the processing speed to 100 milliseconds, checkpointing times go
			up, even though checkpointing has nothing to do with client processing logic.
		* If you upgrade processing speed again to 1000 milliseconds, checkpointing times go up again.
		* This seems to indicate some sort of correlation between processing speed or client logic and the checkpointing
			behaviour.
		* When leaving the test client running overnight or even longer, the checkpointing times get ludicrous, even if the
			application isn't really doing anything. Set the processing speed to 1000 milliseconds and leave the test
			application running overnight, then in the morning check the log for detailed CSV-like global statistics.
			From our tests, we've seen disk checkpoint latencies of 10 and 20 seconds, even though client processing is
			nothing but a Thread.Sleep/Task.Delay.
	* SQL Server lease management seems to outperform the Azure Storage lease manager, especially in the scenario where the test
		application is ran overnight or for a few days. The SQL lease manager sometimes has similar checkpoint latency spikes,
		but the spikes are only half as high and the application recovers from them better and faster.
	* In a different environment we have noticed a lot of issues where leases are lost and the SDK starts to shuffle partitions
		around from one client to the other. Once this starts happening and there is backlog pressure on the Event Hub, it takes
		a long time to recover, as the clients keep shuffling and the shuffling is taking away performance that would otherwise
		be put towards working through the backlog. We have not been able to consistently reproduce any kind of shuffling
		behaviour, other than the intended one-time redistribution of partitions in this test application.

Please note the SQL Lease Manager will attempt to create a lease table in SQL Server based on the lease class' attribute's.
