import { describe, it, expect } from 'vitest';
import {
    RpcSystemCallSender,
    RpcSerializationFormat,
    REMOTE_EXCEPTION_TYPE_REF,
    deserializeMessage,
    genericErrorFilter,
    toExceptionInfo,
} from '../src/index.js';
import type { RpcConnection, RpcExceptionInfo } from '../src/index.js';

function captureConn(): { conn: RpcConnection; sent: () => string } {
    let last = '';
    const conn = {
        send: (s: string) => { last = s; },
        sendBinary: () => { throw new Error('unexpected binary send'); },
        encoder: undefined,
    } as unknown as RpcConnection;
    return { conn, sent: () => last };
}

function sendError(sender: RpcSystemCallSender, error: unknown): RpcExceptionInfo {
    const { conn, sent } = captureConn();
    sender.error(conn, RpcSerializationFormat.get('json5np'), 7, error);
    return deserializeMessage(sent()).args[0] as RpcExceptionInfo;
}

describe('RpcSystemCallSender.errorFilter', () => {
    it('passes the original message through by default', () => {
        const info = sendError(new RpcSystemCallSender(), new Error('/srv/app/secrets.ts failed'));

        expect(info).toEqual({
            TypeRef: REMOTE_EXCEPTION_TYPE_REF,
            Message: 'Error: /srv/app/secrets.ts failed',
        });
    });

    it('replaces the message when genericErrorFilter is set', () => {
        const sender = new RpcSystemCallSender();
        sender.errorFilter = genericErrorFilter;
        const info = sendError(sender, new Error('/srv/app/secrets.ts failed'));

        expect(info.TypeRef).toBe(REMOTE_EXCEPTION_TYPE_REF);
        expect(info.Message).not.toContain('secrets');
        expect(info).toEqual(genericErrorFilter(null, info));
    });

    it('hands a custom filter both the error and the unfiltered info', () => {
        const sender = new RpcSystemCallSender();
        sender.errorFilter = (error, info) => ({
            TypeRef: info.TypeRef,
            Message: error instanceof RangeError ? 'range' : info.Message,
        });

        expect(sendError(sender, new RangeError('out')).Message).toBe('range');
        expect(sendError(sender, new Error('kept')).Message).toBe('Error: kept');
    });

    it('leaves toExceptionInfo unfiltered when no filter is passed', () => {
        expect(toExceptionInfo(new TypeError('bad'))).toEqual({
            TypeRef: REMOTE_EXCEPTION_TYPE_REF,
            Message: 'TypeError: bad',
        });
    });
});
