# pcrjjc-backend

source of backend server for hoshino bot plugin pcrjjc(https://github.com/lulu666lulu/pcrjjc)

This repository contains three parts: PCRAgent, PCRApi and queryapi

**本项目基于AGPL v3协议开源，并且由于项目的特殊性，禁止基于本项目的任何商业行为**

## PCRAgent

PCRAgent aims to obtain uid and access key from the official princonne-redive app with the help of fiddler.

## PCRApi

PCRApi uses uid and access key obtained from PCRAgent to provide in-game query services. The more accounts you have, the faster query will be and the less possibly your accounts will get banned.

## queryapi

queryapi is written in python, which envelops the arena rank query service from PCRApi and made it easy to call in python.

**Please ensure you have set your apiroot correctly**

## Api Documentation

### /enqueue

#### Parameters:

- target_viewer_id: viewer id of which to query

#### Returns:


```json
{
    "request_id": "a uuid representing the current request"
}
```
if `null` is returned, it indicates that the server has queued too much requests and the current request is discarded, and you should wait a few seconds and try again.

### /query

#### Parameters:

- request_id: request_id returned from /enqueue
- full: whether the api will returns full profile data or just arena rank parts.

#### Returns:

- When the request id in queue:
```json
{
    "status": "queue",
    "pos": "position the reqeust in queue"
}
```
- When the request id done:
```json
{
    "status": "done",
    "data": "result"
}
```
- When the request_id is wrong or generated so long ago that it has been removed from cache.
```json
{
    "status": "notfound"
}
```

## Setup Instruction

以下方法已过时，请参考[pcrjjc2](https://github.com/qq1176321897/pcrjjc2)的方法获取access_key和uid

### Obtain the access_key

1. Build and run PCRAgent, make sure port 443 is not occupied by other applications.
2. Bind your android phone or emulator with system version **lower than android 7** to fiddler running on your pc. You can follow the instructions on this [website](https://www.cnblogs.com/softidea/p/6198864.html).
3. Configure the fiddler host remapping. Tools -> HOSTS, click `enabling remapping of ...`, and then type the following text into the textbox and press `Save`:
```
127.0.0.1 le1-prod-all-gs-gzlj.bilibiligame.net
127.0.0.1 l2-prod-all-gs-gzlj.bilibiligame.net
127.0.0.1 l3-prod-all-gs-gzlj.bilibiligame.net
```
4. open the priconne-redive client and the PCRAgent will print your uid and access_key in console.

### Configure the PCRApi

1. write account data into accounts.json. Format is as below:
```json
[
    {
        "uid": "uid1",
        "access_key": "access_key1"
    },
    {
        "uid": "uid2",
        "access_key": "access_key2"
    }
]
```
2. change hosts.txt to configure ports listening to, you can define multiple hosts in multiple lines. Format is as below:
```
http://*:80/
https://*:443/
http://*:8080/
```
3. run PCRApi.exe and enjoy
