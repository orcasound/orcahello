# Managing and Troubleshooting Inference Containers in Azure

This file is structured like a FAQ, with topics around managing
and troubleshooting inference containers running in Azure.
The intended audience is a user relatively new to Kubernetes
who wants a quick reference list of useful commands for various
purposes.

- [Inference Script Logs](#inference-script-logs)
  - [Q: How do I watch the log of a pod in AKS?](#q-how-do-i-watch-the-log-of-a-pod-in-aks)
- [Pods](#pods)
  - [Q: How do I list all inference pods?](#q-how-do-i-list-all-inference-pods)
  - [Q: How do I deploy a pod configuration update?](#q-how-do-i-deploy-a-pod-configuration-update)
  - [Q: How do I see platform version details under a pod?](#q-how-do-i-see-platform-version-details-under-a-pod)
  - [Q: How do I see CPU loads of all pods?](#q-how-do-i-see-cpu-loads-of-all-pods)
  - [Q: How do I deploy a new configmap?](#q-how-do-i-deploy-a-new-configmap)
- [Namespaces](#namespaces)
  - [Q: How do I stop the inference system pods in a namespace?](#q-how-do-i-stop-the-inference-system-pods-in-a-namespace)
  - [Q: How do I see which pods are running on which nodes?](#q-how-do-i-see-which-pods-are-running-on-which-nodes)
- [Agent Pools](#agent-pools)
  - [Q: How do I change the max node count?](#q-how-do-i-change-the-max-node-count)
  - [Q: How do I create a new node pool?](#q-how-do-i-create-a-new-node-pool)
  - [Q: How do I create a debug pod on a given node?](#q-how-do-i-create-a-debug-pod-on-a-given-node)
- [Cores](#cores)
  - [Q: How can I see what cores PID 1 is using?](#q-how-can-i-see-what-cores-pid-1-is-using)
  - [Q: How do I see the CPU % by core in a pod?](#q-how-do-i-see-the-cpu--by-core-in-a-pod)
- [Memory](#memory)
  - [Q: How do I tell how much memory pods use and need on an F4 node?](#q-how-do-i-tell-how-much-memory-pods-use-and-need-on-an-f4-node)
  - [Q: How do I tell if pods are being killed due to OOM?](#q-how-do-i-tell-if-pods-are-being-killed-due-to-oom)
- [Disk Pressure (Ephemeral storage)](#disk-pressure-ephemeral-storage)
  - [Q: How do I get the usage on a node?](#q-how-do-i-get-the-usage-on-a-node)
  - [Q: How do I deal with Disk Pressure?](#q-how-do-i-deal-with-disk-pressure)
- [Billing](#billing)
  - [Q: How do I analyze the costs?](#q-how-do-i-analyze-the-costs)

## Inference Script Logs

### Q: How do I watch the log of a pod in AKS?

The simplest method is to use the Orcanode Monitor dashboard:

1. Go to [orcanodemonitor.azurewebsites.net](https://orcanodemonitor.azurewebsites.net/)
2. Click on the cell in the "OrcaHello Lag" column for the hydrophone of interest

Alternatively, you can see the log using commands on your own machine:

1. Get the pod name from the namespace.  For example to get the pod for `NAMESPACE=andrews-bay`:

```
> kubectl get pods -n $NAMESPACE
NAME                                READY   STATUS    RESTARTS   AGE
inference-system-859f4f4dc7-85wpf   1/1     Running   0          172m
```

2. Get the last (say) 30 lines of the log for that pod using the pod name:

```bash
kubectl logs -n $NAMESPACE inference-system-859f4f4dc7-85wpf --tail=30
```

## Pods

### Q: How do I list all inference pods?

Windows:

```cmd
kubectl get pods --all-namespaces | findstr infer
```

Linux:

```bash
kubectl get pods --all-namespaces | grep infer
```

### Q: How do I deploy a pod configuration update?

Use the following commands, replacing `$NAMESPACE` with the appropriate namespace (e.g., `andrews-bay`).

```bash
kubectl apply -f deploy/$NAMESPACE.yaml
kubectl rollout restart deployment inference-system -n $NAMESPACE
```

### Q: How do I see platform version details under a pod?

1. Get the pod name from the namespace.  For example to get the pod for `NAMESPACE=andrews-bay`:

```
> kubectl get pods -n $NAMESPACE
NAME                                READY   STATUS    RESTARTS   AGE
inference-system-859f4f4dc7-85wpf   1/1     Running   0          172m
```

2. Get an interactive shell in that pod:

```
kubectl exec -it inference-system-859f4f4dc7-85wpf -n $NAMESPACE -- /bin/bash
```

Processor:

```
lscpu
```

Python:
```
python3 --version
```

Torch:
```
python3 -c "import torch; print(torch.__config__.show())"
```

### Q: How do I see CPU loads of all pods?

```
kubectl get pods --all-namespaces -o custom-columns=NAME:.metadata.name,CPU_LIMIT:.spec.containers[*].resources.limits.cpu,CPU_REQUEST:.spec.containers[*].resources.requests.cpu
kubectl top pod --all-namespaces
```

where the CPU(cores) is in milli-cores.  So 929m means 92.9% if a pod has a 1 core configuration limit.
Example:

```
> kubectl top pod --all-namespaces
NAMESPACE        NAME                                             CPU(cores)   MEMORY(bytes)
andrews-bay      benchmark-pod-7c445dfc74-ttqpl                   0m           138Mi
andrews-bay      inference-system-7d679f4ccb-7jqsw                929m         1488Mi
bush-point       inference-system-d8d67c775-4gp2g                 983m         2029Mi
kube-system      ama-logs-glqjn                                   50m          241Mi
kube-system      ama-logs-l4chf                                   64m          276Mi
kube-system      ama-logs-m5gwk                                   58m          258Mi
kube-system      ama-logs-rs-7bfcc69558-4vb2b                     42m          207Mi
kube-system      azure-ip-masq-agent-c9xbp                        1m           20Mi
kube-system      azure-ip-masq-agent-f7zwq                        1m           17Mi
kube-system      azure-ip-masq-agent-rl4p5                        1m           19Mi
kube-system      cloud-node-manager-jlr5h                         1m           20Mi
kube-system      cloud-node-manager-nv7df                         1m           17Mi
kube-system      cloud-node-manager-qklxn                         1m           20Mi
kube-system      coredns-6865d647c6-cdkmg                         6m           34Mi
kube-system      coredns-6865d647c6-gl29g                         6m           35Mi
kube-system      coredns-autoscaler-67d9d668db-nps46              1m           15Mi
kube-system      csi-azuredisk-node-2znl5                         1m           62Mi
kube-system      csi-azuredisk-node-84682                         1m           74Mi
kube-system      csi-azuredisk-node-md4bw                         1m           66Mi
kube-system      csi-azurefile-node-8dhbr                         2m           69Mi
kube-system      csi-azurefile-node-cw8js                         2m           69Mi
kube-system      csi-azurefile-node-dc4l9                         2m           80Mi
kube-system      konnectivity-agent-9d6647f89-f9jdw               4m           26Mi
kube-system      konnectivity-agent-9d6647f89-kprrh               8m           27Mi
kube-system      konnectivity-agent-autoscaler-6ff7779788-vpqhj   2m           18Mi
kube-system      kube-proxy-2x75x                                 2m           45Mi
kube-system      kube-proxy-kjhf6                                 2m           44Mi
kube-system      kube-proxy-ksjqv                                 3m           55Mi
kube-system      metrics-server-5554f5bfbd-4p7h4                  10m          51Mi
kube-system      metrics-server-5554f5bfbd-9cfwl                  7m           50Mi
mast-center      inference-system-6cc784cb6c-n2mwv                984m         2081Mi
orcasound-lab    inference-system-76c476fdc7-zrs6z                820m         1662Mi
point-robinson   inference-system-6487fb8c59-466sz                994m         1873Mi
port-townsend    inference-system-6f84d95d79-mf2t6                804m         2000Mi
sunset-bay       inference-system-66bb79b8c7-x7vdw                826m         2039Mi
```

In the example above, `point-robinson`, `mast-center`, and `andrews-bay` are all pegged.

To see just one namespace and verify the config:

```
kubectl get pods -n $NAMESPACE  -o custom-columns=NAME:.metadata.name,CPU_LIMIT:.spec.containers[*].resources.limits.cpu,CPU_REQUEST:.spec.containers[*].resources.requests.cpu

kubectl top pod -n $NAMESPACE
```

Example using `NAMESPACE=andrews-bay`:

```
> kubectl get pods -n andrews-bay  -o custom-columns=NAME:.metadata.name,CPU_LIMIT:.spec.containers[*].resources.limits.cpu,CPU_REQUEST:.spec.containers[*].resources.requests.cpu
NAME                                CPU_LIMIT   CPU_REQUEST
benchmark-pod-7c445dfc74-ttqpl      1           1
inference-system-7d679f4ccb-7jqsw   1           1

> kubectl top pod -n andrews-bay
NAME                                CPU(cores)   MEMORY(bytes)
benchmark-pod-7c445dfc74-ttqpl      0m           138Mi
inference-system-7d679f4ccb-7jqsw   994m         1401Mi
```

In the above example, the CPU is pegged because 0.994/1 = 99.4% CPU

### Q: How do I deploy a new configmap?

Using (say) `north-sjc` as the namespace:

```
kubectl describe configmap hydrophone-configs -n north-sjc
kubectl apply -f deploy/north-sjc-configmap.yaml
kubectl describe configmap hydrophone-configs -n north-sjc
kubectl rollout restart deployment inference-system -n north-sjc
kubectl rollout status deployment inference-system -n north-sjc
```

## Namespaces

### Q: How do I stop the inference system pods in a namespace?

Use the following commands, making sure `$NAMESPACE` is replaced with the namespace:

```bash
kubectl scale deployment inference-system -n $NAMESPACE --replicas=0
```

Or, remove the existing ones (including errored ones, etc.) and let a new one load:

```bash
kubectl delete pod -n $NAMESPACE -l app=inference-system
```

### Q: How do I see which pods are running on which nodes?

```bash
kubectl get pod -o wide --all-namespaces -l app=inference-system
```

## Agent Pools

### Q: How do I change the max node count?

To change the existing max-count to 4:

```bash
az aks nodepool update --resource-group LiveSRKWNotificationSystem --cluster-name inference-system-AKS --name $POOLNAME --min-count 1 --max-count 4 --update-cluster-autoscaler
az aks nodepool show --resource-group LiveSRKWNotificationSystem --cluster-name inference-system-AKS --name $POOLNAME --query "{autoscaler:enableAutoScaling, min:minCount, max:maxCount}"
```

Or, to enable auto-scaling:

```
az aks nodepool update --resource-group LiveSRKWNotificationSystem --cluster-name inference-system-AKS --name $POOLNAME --min-count 1 --max-count 2 --enable-cluster-autoscaler
```

### Q: How do I create a new node pool?

```
az aks nodepool add --resource-group LiveSRKWNotificationSystem --cluster-name inference-system-AKS --name $POOLNAME --node-vm-size Standard_F4s_v2 --node-count 1 --mode User
az aks nodepool list --resource-group LiveSRKWNotificationSystem  --cluster-name inference-system-AKS -o table
```

To change the node count later:
```
az aks nodepool scale --resource-group LiveSRKWNotificationSystem  --cluster-name inference-system-AKS --name $POOLNAME --node-count 3
```

### Q: How do I create a debug pod on a given node?

```
kubectl debug node/aks-agentpool-41025176-vmss00001q -it --image=ubuntu
lscpu
cat /proc/cpuinfo | grep "model name"
free -h
numactl --hardware
```

and to clean them up later:
```
kubectl delete pod -n default --field-selector status.phase=Succeeded
kubectl delete pod -n default --field-selector status.phase=Failed
```

## Cores

### Q: How can I see what cores PID 1 is using?

```
kubectl get pods --all-namespaces | findstr infer
kubectl exec -it inference-system-547c9699-rcxzs -n andrews-bay -- /bin/bash
ps -L -p 1 -o pid,tid,psr,%cpu,comm --sort=-%cpu
```

### Q: How do I see the CPU % by core in a pod?

```
kubectl exec -it inference-system-547c9699-rcxzs -n andrews-bay -- /bin/bash

top
```

Hit 1 and look at output like:

```
%Cpu0 : 11.7 us, 5.2 sy, 0.0 ni, 81.4 id, 0.0 wa, 0.0 hi, 1.7 si, 0.0 st 
%Cpu1 : 48.5 us, 2.2 sy, 0.0 ni, 47.0 id, 0.0 wa, 0.0 hi, 2.2 si, 0.0 st 
%Cpu2 : 77.6 us, 3.3 sy, 0.0 ni, 18.3 id, 0.0 wa, 0.0 hi, 0.8 si, 0.0 st 
%Cpu3 : 61.5 us, 2.7 sy, 0.0 ni, 33.9 id, 0.0 wa, 0.0 hi, 1.9 si, 0.0 st
```

Take 100-<id(le)> so 81.4% idle, 47% idle, etc.

## Memory

### Q: How do I tell how much memory pods use and need on an F4 node?

To see the current CPU and memory use of each node:
```
kubectl top node
```

To see what pods are currently running vs pending:
```
kubectl get pod -o wide --all-namespaces | findstr inference-system
```

If one is Pending:
```
kubectl describe pod inference-system-59d785488-45p7n -n andrews-bay
```

How do I see memory request of each running pod?
```
kubectl get pods --all-namespaces -o custom-columns=NAME:.metadata.name,MEM_REQUEST:.spec.containers[*].resources.requests.memory,MEM_LIMIT:.spec.containers[*].resources.limits.memory | findstr infer
```

Example:
```
inference-system-59d785488-45p7n                        1700Mi           1700Mi
inference-system-5d45857b77-n6zfm                       3Gi              3Gi
inference-system-557bcf76dd-5rvqb                       1700Mi           1700Mi
inference-system-6d6f8bd78c-hmtz5                       1900Mi           1900Mi
inference-system-557bcf76dd-f2xhb                       1700Mi           1700Mi
inference-system-5d45857b77-nxg4l                       3Gi              3Gi
inference-system-589ddb4546-5bbc6                       3G               3G
```

To see memory usage per pod:

```bash
kubectl top pod --all-namespaces
```

To see why a node is utilized:

```bash
kubectl describe node aks-f4sv2pool-22767839-vmss000000
```

To change the limits, edit the `deploy/andrews-bay.yaml` file

```cmd
kubectl scale deployment inference-system -n andrews-bay --replicas=0
kubectl apply -f deploy\andrews-bay.yaml
kubectl describe node aks-f4sv2pool-22767839-vmss000001 | findstr memory
```

### Q: How do I tell if pods are being killed due to OOM?

```
kubectl get events -A --sort-by=.metadata.creationTimestamp
kubectl top pod --all-namespaces --sort-by=memory
```

## Disk Pressure (Ephemeral storage)

### Q: How do I get the usage on a node?

```cmd
kubectl describe node aks-f4sv2pool-22767839-vmss000000 | findstr /i ephemeral-storage
  ephemeral-storage:  129886128Ki	<- allocatable
  ephemeral-storage:  119703055367	<- capacity
  ephemeral-storage  0 (0%)        0 (0%)	<- current usage (incorrect)
kubectl describe node aks-f4sv2pool-22767839-vmss000000 | findstr Pressure
kubectl debug node/aks-f4sv2pool-22767839-vmss000000 -it --image=ubuntu
chroot /host
du -sh /var/lib/kubelet/pods/* | sort -h
du -sh /var/log/pods/* | sort -h
# ls -l
total 7552
-rw-r----- 1 root root 7725673 Dec 12 03:24 0.log
# pwd
/var/log/pods/mast-center_inference-system-5bb97c5889-h22lf_dcb5c816-e25c-47a9-9897-eb4324e3dd36/inference-system
```

### Q: How do I deal with Disk Pressure?

```bash
kubectl debug node/aks-f4sv2pool-22767839-vmss000000 -it --image=ubuntu --profile=general
du -sh /host/var/lib/containerd/* | sort -h
```

This might show lines like this:
```
19G     /host/var/lib/containerd/io.containerd.content.v1.content
46G     /host/var/lib/containerd/io.containerd.snapshotter.v1.overlayfs
```

Also check:
```bash
du -sh /host/var/lib/kubelet/pods/* | sort -h
du -sh /host/var/log/* | sort -h

chroot /host
crictl image prune
```

To recycle the node:
```bash
kubectl cordon aks-f4sv2pool-22767839-vmss000000
kubectl drain aks-f4sv2pool-22767839-vmss000000 --ignore-daemonsets --delete-emptydir-data --force
az aks nodepool show --resource-group LiveSRKWNotificationSystem --cluster-name inference-system-AKS --name f4sv2pool --query count -o tsv
```
	to get <n>, so if <n> is 2 then:

```bash
az aks nodepool scale --resource-group LiveSRKWNotificationSystem --cluster-name inference-system-AKS --name f4sv2pool --node-count 1
az aks nodepool scale --resource-group LiveSRKWNotificationSystem --cluster-name inference-system-AKS --name f4sv2pool --node-count 2
```

## Billing

### Q: How do I analyze the costs?

1. Log into [portal.azure.com](https://portal.azure.com/)
2. Click "Subscriptions" and then "Microsoft Azure Sponsorship 2"
3. Expand "Cost Management" in the left bar and select "Cost analysis"
