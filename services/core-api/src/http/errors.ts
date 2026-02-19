export class HttpError extends Error {
    constructor(
      public status: number,
      message: string,
      public details?: Record<string, unknown>
    ) {
      super(message);
    }
  }
  
  export function notFound(message: string, details?: Record<string, unknown>) {
    return new HttpError(404, message, details);
  }
  export function conflict(message: string, details?: Record<string, unknown>) {
    return new HttpError(409, message, details);
  }
  export function unprocessable(message: string, details?: Record<string, unknown>) {
    return new HttpError(422, message, details);
  }