import express from "express";
import { executionsRouter } from "./routes/executions.js";
import { nodesRouter } from "./routes/nodes.js";

export const router = express.Router();

router.use("/executions", executionsRouter);
router.use(nodesRouter);
