# Quick Notes

# Requirements
This program should be able to process multiple concurrent **DagRequests** and also have the ability to run the **Nodes** in each **DagRequest** in parallel on machines with the constraint on execution order in place.

In addition to these requirements, I also brought up in discussion whether there is a default retry in place in case of a failed execution. And if there is a rate limiter in place to handle the incoming requests to the server in case too many requests are being pushed onto very few machines in comparison. I was told not to worry about them , and if I want, I can implement these two requirements.

# My Approach 

In order to handle concurrent node execution as well as processing concurrent requests, I had two ideas on how to solve this. 

## Two approaches 

1. Using a **task queue** and let each request add/remove the node they want to execute/cancel via the task queue.  **Push Model**
2. Using a **machine pool**, and let each request attempt to pull an available machine to execute their next node if there is one.  **Pull Model**

## Pros/Cons
Approach 1:  The task queue manages the machine pool and does not contain any logic on the order of node that should get executed except honoring the order of how they got added. 

Also, this task queue can be implemented further as a prioritized task queue with weighted node to support further adjustments on execution order.

This approach is the centralized task/machine pool management. It provides more flexibility to support priority management/machine pool monitoring/customized sets of machines for different requests. In case a machine keeps return failed nod executions, task queue will be able to detect such situation.  

 VS.
 
Approach 2:  This approach does not honor the order of the requests that came in, since the last request had the equal opportunity of grabbing a machine in the machine pool as the first one. Also if there are too many requests coming in at the same time,  it will take a long time to even finish one request. The chance of a request can grab a machine decreases as more requests coming. 

## Chose Approach 1
There are work arounds to solve the problem I mentioned in Approach 2 such as set a limit on the number of requests can pull from the machine pool, and let the other requests wait( similar to a blocking queue). 

Since I asked that this exercise was intended to design for  a machine size of around 10 (behaving more a thread pool), and approach 1 can better guarantee a best effort behavior (we try to finish processing the request that comes first). I decided to go with approach 1.

## Implementation level detail
In addition to the provided interfaces/classes, I add two main components:

Dag related classes: **Node**, **DAG** as the data structures to store the underlying Dag as an adjacent list of edges.

**DAG**: This class serve as the underlying internal request class since the DagRequest/RequestProcessor are user facing classes with *public* APIs. It has the dag graph stored in place, and handles the majority of the logic on 
1. keep track of different states of the node (not started, running, completed, cancelled) 
2. processing the result of a node execution
3. keep track of which nodes are ready to get added to the queue
4. the retry logic for each node
5. notify the subscriber/return the result for the Task[RequestResponse] once the request is done.

**TaskQueue**: This is the main class to create and maintain the machine pool, and handle the actual execution of nodes on machines. It does not have dependency on the DagRequest itself, only is aware of the Node as a unit of execution work. If there are adjustments/data structure changes made to the DagRequest, there will not be any major change to the TaskQueue class itself.

TaskQueue is a singleton owned by the RequestProcessor that gets passed to each DagRequest( In c++, the implementation will be a shared_ptr that is guaranteed not to be null otherwise fail_fast :(  

There are additional methods that can be implemented to make it more production-code ready. For example: 
shutdown() ( remove all nodes in the task queue), 
using a mutex lock on the task queue as a list( instead of the c# concurrentQueue which does not support remove by index), 
suspend()/resume() (let the current running nodes finished on the machines, but do not queue additional nodes)
size() returns the current size of the task queue. 

Due to the time constraints, I will just mention them here :)

*There are several other classes to help with validating the graph and parsing xml, but is not part of the main logic.*

## Failure Handling
For failure handling, we discussed/clarified the requirement as such:
If a node ultimately failed after retries, the DagRequest is considered not complete fully and we can abort the execution of all nodes.

### Abort Request
In my implementation, once the node failed after retries, abortRequest is called. The abort steps is as following 
1. the atomic boolean (aborted) is set so guranteed no further node get added to the machine,  and do not start the abort process twice in case another node failed and tries to call abortRequest in parallel from a different thread.
2. mark the nodes that are waiting to be queued as cancelled.
3. call RemoveNode on nodes that got added to TaskQueue: 
	1. if the node has not started on the machine, we simply remove it from the queue return true in this case.
	2. if the node has started on the machine, no-op , let it finish running, return false..
	 In this case, we can introduce a cancellation token to cancel the running node on the machine as well. However, from the problem description and the API it was not clear whether it is possible to do so. 

4. Return the Task for this DagRequest as failed.  One behavior decision I made here is to let the user know about the failure ASAP. 
	1. An alternative approach could be if there are still nodes running on the machine ( see my remark on Step 3.2 ), wait till (the did not successfully removed) nodes from taskqueue complete to set the request failed. we can have a final partial result if the user may use the partial result.
	
### Retry
In my program, retry number is set via the RequestProcessor since the DagXML interface didn't include such attribute. However, ideally for each request/each node in the request, user should be able to set the retry number individually.  My code does support such function with minimal code change.

# Tests
I only included few sets of test which can be expanded vertically: 
1. Integration tests: set different parameters for cases:
	1. Setting the machine pool size to be small with high failure rate to check if retry set to 0 means high failure rate vs. enable retry to mitigate the failures.
2. Functional tests:  sending DagRequest from different threads
3. Unit tests: adding/removing nodes from TaskQueue, abort request on DAG etc.
4. Stress test: Sending large number of requests with large number of nodes.
5. Input test: make sure we don't process invalid dag request. (cycle, wrong node info)

# Machine Pool Management
For this problem, I did not implement a machine pool class, only let the task queue behaves in such a way that it creates and matins the machine. Ideally, there can be a separate machine pool class that specifically handles giving out a available machine, machine health monitoring, life cycle management.
(Think as a ThreadPool)