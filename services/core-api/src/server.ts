/**
 * HTTP Server
 */
import express from "express";
import { router } from "./presentation/http/routes.js";
import { errorMiddleware } from "./presentation/http/error-handler.js";

const app = express();
app.use(express.json({ limit: "1mb" }));

app.get("/health", (_req, res) => res.json({ ok: true }));

app.use(router);
app.use(errorMiddleware);

const port = Number(process.env.PORT ?? 8080);
app.listen(port, () => {
  console.log(`core-api listening on :${port}`);
});
