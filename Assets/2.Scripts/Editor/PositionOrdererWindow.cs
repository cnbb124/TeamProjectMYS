using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace PositionOrder {

    public class PositionOrdererWinodw : EditorWindow {

        private enum OrderType { Line, Table, Cube }

        private PositionOrderer orderer = new PositionOrderer ();

        private OrderType orderType;
        private Axis2D axis2D;

        private LineAnchor lineAnchor;
        private TableAnchor tableAnchor;
        private CubeAnchor cubeAnchor;

        private int customIndex;
        private int colCount;
        private int rowCount;

        private int selectedIndex = -1;
        private bool isUpdate = false; 

        private static float minHeight = 336;

        private Vector2 startPos = new Vector2 (0, 0); 
        private float defaultContentHeight = 156; 
        private float contentHeightWithoutList = 156; 

        [MenuItem ("Window/Position Orderer")]
        private static void Init () {
            PositionOrdererWinodw window = GetWindow (typeof (PositionOrdererWinodw), true, "Position Orderer") as PositionOrdererWinodw;
            window.minSize = new Vector2 (300, minHeight);
            window.Show ();
        }

        private void OnSelectionChange () {
         
            if (Selection.activeGameObject != null) {

                if (orderer.Transforms.Contains (Selection.activeGameObject.transform)) {
                    customIndex = orderer.Transforms.IndexOf (Selection.activeGameObject.transform);
                    selectedIndex = customIndex;
                    Repaint (); 
                }
            }
        }

        private void OnGUI () {

            #region Variables

            float scrollSizeX = position.width - startPos.x;
            float scrollSizeY = position.height - contentHeightWithoutList;

            Color color_default = GUI.backgroundColor;
            Color color_selected = new Color (0.85f, 0.85f, 0.85f);

            GUIStyle itemStyle = new GUIStyle (GUI.skin.button);
            itemStyle.alignment = TextAnchor.MiddleLeft;
            itemStyle.active.background = itemStyle.normal.background;
            itemStyle.margin = new RectOffset (0, 0, 0, 0);

            GUIStyle boldStyle = new GUIStyle (GUI.skin.label);
            boldStyle.fontStyle = FontStyle.Bold;

            #endregion

            #region Object List Management

            GUI.DrawTexture (new Rect (0, 20, scrollSizeX, scrollSizeY), MakeTex ((int)scrollSizeX, (int)scrollSizeY, color_selected));

            GUIContent transformListLabel = new GUIContent ("Transform List", "Select objects and click Add button to add transforms into list");
            GUILayout.Label (transformListLabel, boldStyle);

            startPos = EditorGUILayout.BeginScrollView (startPos, GUILayout.Width (scrollSizeX), GUILayout.Height (scrollSizeY));

            for (int i = 0; i < orderer.Transforms.Count; i++) {
                GUI.backgroundColor = (selectedIndex == i) ? color_selected : Color.clear;

                if (orderer.Transforms[i] != null) {
                    if (GUILayout.Button (string.Format("[{0}] {1}", i, orderer.Transforms[i].name), itemStyle)) {
                        selectedIndex = i;
                        Selection.activeGameObject = orderer.Transforms[i].gameObject; 
                    }

                } else {
                    orderer.Transforms.RemoveAt (i);
                }

                GUI.backgroundColor = color_default;
            }

            EditorGUILayout.EndScrollView ();


            EditorGUILayout.BeginHorizontal ();

            if (GUILayout.Button ("Add")) {
                isUpdate = false;

                GameObject[] objs = Selection.gameObjects;
                Array.Sort (objs, new UnityTransformSort ());

                foreach (var obj in objs) {
                    if (!orderer.Transforms.Contains (obj.transform)) {
                        orderer.Transforms.Add (obj.transform);
                    }
                }
            }

            if (GUILayout.Button ("Remove")) { 
                if (selectedIndex >= 0 && selectedIndex < orderer.Transforms.Count) {
                    isUpdate = false;
                    orderer.Transforms.RemoveAt (selectedIndex);
                    selectedIndex -= 1;
                }
            }

            if (GUILayout.Button ("Clear")) { 
                isUpdate = false;
                orderer.Transforms.Clear ();
                selectedIndex = -1;
            }

            EditorGUILayout.EndHorizontal ();

            #endregion

            #region Draw Order Type Elements

            GUILayout.Space (18f);

            DrawLabelElement ("Type", 50, () => SetOrderType ((OrderType)EditorGUILayout.EnumPopup (orderType)));

            if (orderType == OrderType.Line) { 
                DrawLabelElement ("Anchor", 50, () => SetLineAnchor ((LineAnchor)EditorGUILayout.EnumPopup (lineAnchor)));

                if (lineAnchor == LineAnchor.Custom) { 
                    DrawLabelElement ("Index", 50, () => customIndex = EditorGUILayout.IntField (customIndex));
                }

            } else if (orderType == OrderType.Table) {
                DrawLabelElement ("Anchor", 50, () => SetTableAnchor ((TableAnchor)EditorGUILayout.EnumPopup (tableAnchor)));
                DrawLabelElement ("Axis", 50, () => axis2D = (Axis2D)EditorGUILayout.EnumPopup (axis2D));

                if (tableAnchor == TableAnchor.Custom) { 
                    DrawLabelElement ("Index", 50, () => customIndex = EditorGUILayout.IntField (customIndex));
                }

                DrawLabelElement ("Column", 50, () => colCount = EditorGUILayout.IntField (colCount));

            } else if (orderType == OrderType.Cube) { 
                DrawLabelElement ("Anchor", 50, () => SetCubeAnchor ((CubeAnchor)EditorGUILayout.EnumPopup (cubeAnchor)));

                if (cubeAnchor == CubeAnchor.Custom) {
                    DrawLabelElement ("Index", 50, () => customIndex = EditorGUILayout.IntField (customIndex));
                }


                EditorGUILayout.BeginHorizontal ();

                DrawLabelElement ("Column", 50, () => colCount = EditorGUILayout.IntField (colCount));
                DrawLabelElement ("Row", 30, () => rowCount = EditorGUILayout.IntField (rowCount));

                EditorGUILayout.EndHorizontal ();
            }

            #endregion

            #region Apply Settings

            GUILayout.Space (18f);

            if (isUpdate) { 
                if (orderType == OrderType.Line) {
                    DrawFloatFields (true, true, true);

                } else if (orderType == OrderType.Table) {
                    switch (axis2D) {
                        case Axis2D.XY:
                            DrawFloatFields (true, true, false);
                            break;
                        case Axis2D.XZ:
                            DrawFloatFields (true, false, true);
                            break;
                        case Axis2D.ZY:
                            DrawFloatFields (false, true, true);
                            break;
                    }

                } else if (orderType == OrderType.Cube) {
                    DrawFloatFields (true, true, true);
                }

            } else { 
                EditorGUILayout.BeginHorizontal ();

                if (orderType == OrderType.Line) {
                    DrawFloatFieldsWithFixedLabel (true, true, true);

                } else if (orderType == OrderType.Table) {
                    switch (axis2D) {
                        case Axis2D.XY:
                            DrawFloatFieldsWithFixedLabel (true, true, false);
                            break;
                        case Axis2D.XZ:
                            DrawFloatFieldsWithFixedLabel (true, false, true);
                            break;
                        case Axis2D.ZY:
                            DrawFloatFieldsWithFixedLabel (false, true, true);
                            break;
                    }

                } else if (orderType == OrderType.Cube) {
                    DrawFloatFieldsWithFixedLabel (true, true, true);
                }

                EditorGUILayout.EndHorizontal ();
            }

           
            EditorGUILayout.BeginHorizontal ();

            if (GUILayout.Button ("Update")) { 
                if (orderer.Transforms.Count >= PositionOrderer.MIN_COUNT) {
                    isUpdate = true;
                    SetContentHeight ();

                } else {
                    Debug.LogWarning ("Transform count in list is too small.");
                }
            }

            if (isUpdate) {
                if (GUILayout.Button ("Stop")) {
                    isUpdate = false;
                    SetContentHeight ();
                }

            } else { 
                if (GUILayout.Button ("Apply")) {
                    if (orderer.Transforms.Count >= PositionOrderer.MIN_COUNT) {
                        ApplyOrder ();

                    } else {
                        Debug.LogWarning ("Transform count in list is too small.");
                    }
                }
            }

            EditorGUILayout.EndHorizontal ();

            #endregion
        }

        #region Draw Element

      
        private void DrawFloatFields (bool x, bool y, bool z) {
            if (x)
                SetDistX (EditorGUILayout.FloatField ("X", orderer.Distance_X));
            if (y)
                SetDistY (EditorGUILayout.FloatField ("Y", orderer.Distance_Y));
            if (z)
                SetDistZ (EditorGUILayout.FloatField ("Z", orderer.Distance_Z));
        }

       
        private void DrawFloatFieldsWithFixedLabel (bool x, bool y, bool z) {
            if (x) {
                GUILayout.Label ("X", GUILayout.Width (15));
                SetDistX (EditorGUILayout.FloatField (orderer.Distance_X));
            }

            if (y) {
                GUILayout.Label ("Y", GUILayout.Width (15));
                SetDistY (EditorGUILayout.FloatField (orderer.Distance_Y));
            }

            if (z) {
                GUILayout.Label ("Z", GUILayout.Width (15));
                SetDistZ (EditorGUILayout.FloatField (orderer.Distance_Z));
            }
        }

       
        private void DrawLabelElement (string label, float width, Action draw) {
            if (draw == null)
                return;

            EditorGUILayout.BeginHorizontal ();
            GUILayout.Label (label, GUILayout.Width (width));
            draw ();
            EditorGUILayout.EndHorizontal ();
        }

        #endregion

        #region Setter

        
        private void SetOrderType (OrderType type) {
            this.orderType = type;
            SetContentHeight ();
        }

       
        private void SetLineAnchor (LineAnchor anchor) {
            this.lineAnchor = anchor;
            SetContentHeight ();
        }

        
        private void SetTableAnchor (TableAnchor anchor) {
            this.tableAnchor = anchor;
            SetContentHeight ();
        }

        
        private void SetCubeAnchor (CubeAnchor anchor) {
            this.cubeAnchor = anchor;
            SetContentHeight ();
        }

        
        private void SetDistX (float value) {
            orderer.Distance_X = value;
            if (isUpdate) {
                ApplyOrder ();
            }
        }

       
        private void SetDistY (float value) {
            orderer.Distance_Y = value;
            if (isUpdate) {
                ApplyOrder ();
            }
        }

        
        private void SetDistZ (float value) {
            orderer.Distance_Z = value;
            if (isUpdate) {
                ApplyOrder ();
            }
        }

        
        private void SetContentHeight () {
            float value = 0;

            if (!isUpdate) {
                if (orderType == OrderType.Line) {
                    if (lineAnchor == LineAnchor.Custom)
                        value += 18;

                } else if (orderType == OrderType.Table) {
                    value += 36;
                    if (tableAnchor == TableAnchor.Custom)
                        value += 18;

                } else if (orderType == OrderType.Cube) {
                    value += 18;
                    if (cubeAnchor == CubeAnchor.Custom)
                        value += 18;
                }

            } else {
                if (orderType == OrderType.Line) {
                    value += 36;
                    if (lineAnchor == LineAnchor.Custom)
                        value += 18;

                } else if (orderType == OrderType.Table) {
                    value += 54;
                    if (tableAnchor == TableAnchor.Custom)
                        value += 18;

                } else if (orderType == OrderType.Cube) {
                    value += 54;
                    if (cubeAnchor == CubeAnchor.Custom)
                        value += 18;
                }
            }

            contentHeightWithoutList = defaultContentHeight + value;
            minSize = new Vector2 (300, minHeight + value);
        }

        #endregion

       
        private void ApplyOrder () {
            if (orderType == OrderType.Line) { 
                orderer.ApplyLineOrder (lineAnchor, customIndex);

            } else if (orderType == OrderType.Table) { 
                orderer.ApplyTableOrder (tableAnchor, axis2D, colCount, customIndex);

            } else if (orderType == OrderType.Cube) { 
                orderer.ApplyCubeOrder (cubeAnchor, colCount, rowCount, customIndex);
            }
        }

       
        private Texture2D MakeTex (int width, int height, Color col) {
            Color[] pix = new Color[width * height];

            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;

            Texture2D result = new Texture2D (width, height);
            result.SetPixels (pix);
            result.Apply ();

            return result;
        }
    }

   
    public class UnityTransformSort : IComparer<GameObject> {
        public int Compare (GameObject lhs, GameObject rhs) {
            if (lhs == rhs) 
                return 0;
            if (lhs == null) 
                return -1;
            if (rhs == null)
                return 1;
            return (lhs.transform.GetSiblingIndex () > rhs.transform.GetSiblingIndex ()) ? 1 : -1;
        }
    }
}
