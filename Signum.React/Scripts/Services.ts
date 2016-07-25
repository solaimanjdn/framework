﻿/// <reference path="../typings/whatwg-fetch/whatwg-fetch.d.ts" />
import { ModelState } from './Signum.Entities'
import { Dic } from './Globals'
import { GraphExplorer } from './Reflection'


export interface AjaxOptions {
    url: string;
    avoidNotifyPendingRequests?: boolean;
    avoidThrowError?: boolean;
    avoidGraphExplorer?: boolean;
    avoidAuthToken?: boolean;

    
    headers?: HeaderInit | { [index: string]: string };
    mode?: string | RequestMode;
    credentials?: string | RequestCredentials;
    cache?: string | RequestCache;
}


export function baseUrl(options: AjaxOptions): string {
    const baseUrl = window["__baseUrl"] as string;

    if (options.url.startsWith("~/"))
        return baseUrl + "/" + options.url.after("~/");

    return options.url;
}

export function ajaxGet<T>(options: AjaxOptions): Promise<T> {
    return ajaxGetRaw(options)
        .then(a=> a.status == 204 ? null : a.json<T>());
}

export function ajaxGetRaw(options: AjaxOptions) : Promise<Response> {
    return wrapRequest(options, () =>
        fetch(baseUrl(options), {
            method: "GET",
            headers: Dic.extend({
                'Accept': 'application/json',
            }, options.headers),
            mode: options.mode,
            credentials: options.credentials || "same-origin",
            cache: options.cache
        }));
}

export function ajaxPost<T>(options: AjaxOptions, data: any): Promise<T> {
    return ajaxPostRaw(options, data)
        .then(a=> a.status == 204 ? null : a.json<T>());
}


export function ajaxPostRaw(options: AjaxOptions, data: any) : Promise<Response> {
    if (!options.avoidGraphExplorer) {
        GraphExplorer.propagateAll(data);
    }

    return wrapRequest(options, () =>
        fetch(baseUrl(options), {
            method: "POST",
            credentials: options.credentials || "same-origin",
            headers: Dic.extend({
                'Accept': 'application/json',
                'Content-Type': 'application/json'
            }, options.headers),
            mode: options.mode,
            cache: options.cache,
            body: JSON.stringify(data),
        }));
}


export function wrapRequest(options: AjaxOptions, makeCall: () => Promise<Response>): Promise<Response>
{
    if (!options.avoidThrowError) {
        var call = makeCall;
        makeCall = () => ThrowErrorFilter.throwError(call);
    }

    if (!options.avoidAuthToken && AuthTokenFilter.addAuthToken) {
        let call = makeCall;
        makeCall = () => AuthTokenFilter.addAuthToken(options, call);
    }

    if (!options.avoidNotifyPendingRequests) {
        let call = makeCall;
        makeCall = () => NotifyPendingFilter.onPendingRequest(call);
    }
    
    var promise = makeCall();

    if (!(promise as any).__proto__.done)
        (promise as any).__proto__.done = Promise.prototype.done;

    return promise;

}

export module AuthTokenFilter {
    export let addAuthToken: (options: AjaxOptions, makeCall: () => Promise<Response>) => Promise<Response>;
}


export module NotifyPendingFilter {
    export let notifyPendingRequests: (pendingRequests: number) => void = () => { };
    let pendingRequests: number = 0;
    export function onPendingRequest(makeCall: () => Promise<Response>): Promise<Response> {

        notifyPendingRequests(++pendingRequests);

        return makeCall().then(
            resp => { notifyPendingRequests(--pendingRequests); return resp; },
            error => { notifyPendingRequests(--pendingRequests); throw error; });
    }
}

export module ThrowErrorFilter { 

    export function throwError(makeCall: () => Promise<Response>): Promise<Response> {

        return makeCall().then(response => {
            if (response.status >= 200 && response.status < 300) {
                return response;
            } else {
                return response.json().then((json: WebApiHttpError) => {
                    if (json.ModelState)
                        throw new ValidationError(response.statusText, json);
                    else if (json.Message)
                        throw new ServiceError(response.statusText, response.status, json);
                }) as any;
            }
        });
    }
}

let a = document.createElement("a");
document.body.appendChild(a);
a.style.display = "none";


export function saveFile(response: Response) {
    let fileName = "file.dat";
    let match = /attachment; filename=(.+)/.exec(response.headers.get("Content-Disposition"));
    if (match)
        fileName = match[1].trimEnd("\"").trimStart("\"");

    response.blob().then(blob => {
        
        if (window.navigator.msSaveBlob)
            window.navigator.msSaveBlob(blob, fileName);
        else {
            var url = window.URL.createObjectURL(blob);
            a.href = url;


            (a as any).download = fileName;

            a.click();

            setTimeout(() => window.URL.revokeObjectURL(url), 500);
        }
    });
}

export class ServiceError extends Error {
    constructor(
        public statusText: string,
        public status: number,
        public httpError: WebApiHttpError) {
        super(httpError.ExceptionMessage)
    }

    get defaultIcon() {
        switch (this.httpError.ExceptionType) {
            case "UnauthorizedAccessException": return "glyphicon-lock";
            case "EntityNotFoundException": return "glyphicon-trash";
            case "UniqueKeyException": return "glyphicon-duplicate";
            default: return "glyphicon-alert";
        }
    }

    toString() {
        return this.message;
    }
}

export interface WebApiHttpError {
    Message?: string;
    ModelState?: { [member: string]: string }
    ExceptionMessage?: string;
    ExceptionType?: string;
    StackTrace?: string;
    MessageDetail?: string;
    ExceptionID?: string;
}

export class ValidationError extends Error {
    modelState: ModelState;
    message: string;

    constructor(public statusText: string, json: WebApiHttpError) {
        super(statusText)
        this.message = json.Message;
        this.modelState = json.ModelState;
    }

    toString() {
        return this.statusText + "\r\n" + this.message;
    }
}



//http://blog.guya.net/2015/06/12/sharing-sessionstorage-between-tabs-for-secure-multi-tab-authentication/
//To share session storage between tabs
window.addEventListener("storage", se => {

    if (se.key == 'getSessionStorage') {
        // Some tab asked for the sessionStorage -> send it

        localStorage.setItem('sessionStorage', JSON.stringify(sessionStorage));
        localStorage.removeItem('sessionStorage');

    } else if (se.key == 'sessionStorage' && !sessionStorage.length) {
        // sessionStorage is empty -> fill it

        var data = JSON.parse(se.newValue);

        for (let key in data) {
            sessionStorage.setItem(key, data[key]);
        }
    }
});

if (!sessionStorage.length) {
    // Ask other tabs for session storage
    localStorage.setItem('getSessionStorage', new Date().toString());
};
