# Focus

Simple CLI tool to crawl a site and log the responses.

## Clone and Build
```shell
git clone https://github.com/nagilum/focus
cd Focus
dotnet build -c Release
```

## Usage

```shell
focus https://example.com
```

<p align="center"><img src="screenshot.png?raw=true" alt="Focus Screenshot"></p>

## Parameters

### Set Rendering Engine

Focus uses Playwright behind the scenes for all HTML related requests.
You can select between using `chromium`, `firefox`, and `webkit` as the rendering engine to use.
Focus defaults to using `chromium`.
To set the rendering engine, use the `-e` option.

```shell
focus https://example.com -e firefox
```

*This will set the rendering engine to `Firefox`.*

### Set Retry Attempts

You can set it so that Focus will retry failed requests `n` number of times.
A failed request is either where an error caused it to not complete, or if the response HTTP status code is not in the 2xx range.
By default Focus will not retry failed requests.
To set retry atttempt, use the `-r` option.

```shell
focus https://example.com -r 1
```

*This will retry all failed requests `1` time.*

### Set Request Timeout

You can set the request timeout for all requests.
The default timeout is `10` seconds.
Set the timeout to `0` to disable it.
To set a new timeout, use the `-t` option.

```shell
focus https://example.com -t 3
```

*This will set the request timeout to `3` seconds.*

```shell
focus https://example.com -t 0
```

*This will disable the timeout feature.*

### Add Multiple URLs

You can setup Focus to crawl more than one URL, by simply adding more URLs to the parameter list.

```shell
focus https://example.com https://another-domain.com https://example.com/some-hidden-page
```

This will add those 3 URLs to the queue from the get-go and crawl from there.